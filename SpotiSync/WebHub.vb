Imports System.Collections.Concurrent
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Channels
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Logging

''' <summary>A playback command sent by a browser (play, pause, toggle, stop, next, prev, seek, volume).</summary>
Public Class WebCommand
    Public Property Action As String
    Public Property PositionMs As Long
    Public Property Volume As Integer
    Public Property From As String
End Class

''' <summary>
''' The web server every room's browser connects to. Serves the player page and, over one WebSocket per
''' browser, streams audio chunks, playback state and clock-sync replies, and receives playback commands.
''' </summary>
Public Class WebHub

    Public Class Listener
        Private Shared _nextId As Integer

        Friend Sub New(remoteAddress As String)
            Id = Interlocked.Increment(_nextId)
            Me.RemoteAddress = remoteAddress
            ConnectedAt = DateTime.Now
        End Sub

        Public ReadOnly Property Id As Integer
        Public ReadOnly Property RemoteAddress As String
        Public ReadOnly Property ConnectedAt As DateTime
        Public Property Name As String = ""
        Public Property Listening As Boolean

        ' ~10 s of audio; the oldest messages are dropped for a browser that can't keep up.
        Friend ReadOnly Outbox As Channel(Of Outgoing) = Channel.CreateBounded(Of Outgoing)(
            New BoundedChannelOptions(500) With {.FullMode = BoundedChannelFullMode.DropOldest, .SingleReader = True})
    End Class

    Friend Structure Outgoing
        Public Data As Byte()
        Public IsText As Boolean
    End Structure

    Private ReadOnly _listeners As New ConcurrentDictionary(Of Integer, Listener)
    ' Static files embedded in the exe: URL path -> (content type, bytes).
    Private ReadOnly _files As New Dictionary(Of String, (ContentType As String, Data As Byte()))
    Private _app As WebApplication
    Private _latestState As Byte()
    Private _latencyMs As Integer = 800

    ''' <summary>Raised on a thread-pool thread.</summary>
    Public Event CommandReceived As EventHandler(Of WebCommand)
    ''' <summary>A browser connected, disconnected or changed its room name. Raised on a thread-pool thread.</summary>
    Public Event ListenersChanged As EventHandler

    Public Sub New()
        Dim index = ReadResource("SpotiSync.index.html")
        _files("/") = ("text/html; charset=utf-8", index)
        _files("/index.html") = ("text/html; charset=utf-8", index)
        _files("/icon.svg") = ("image/svg+xml", ReadResource("SpotiSync.icon.svg"))
        _files("/icon-180.png") = ("image/png", ReadResource("SpotiSync.icon-180.png"))
        _files("/favicon.ico") = ("image/x-icon", ReadResource("SpotiSync.favicon.ico"))
    End Sub

    Private Shared Function ReadResource(name As String) As Byte()
        Using stream = GetType(WebHub).Assembly.GetManifestResourceStream(name),
              buffer As New IO.MemoryStream()
            stream.CopyTo(buffer)
            Return buffer.ToArray()
        End Using
    End Function

    Public ReadOnly Property Listeners As Listener()
        Get
            Return _listeners.Values.OrderBy(Function(l) l.Id).ToArray()
        End Get
    End Property

    ''' <summary>
    ''' How far behind the host every browser plays, in ms. The same for all rooms so they stay in sync;
    ''' larger values survive worse Wi-Fi.
    ''' </summary>
    Public Property LatencyMs As Integer
        Get
            Return Volatile.Read(_latencyMs)
        End Get
        Set(value As Integer)
            Volatile.Write(_latencyMs, value)
            Broadcast(ConfigMessage(), isText:=True)
        End Set
    End Property

    Public Async Function StartAsync(port As Integer) As Task
        Dim builder = WebApplication.CreateSlimBuilder()
        builder.Logging.ClearProviders()
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}")
        Dim app = builder.Build()
        app.UseWebSockets()
        RunExtensions.Run(app, AddressOf HandleRequestAsync)
        Await app.StartAsync()
        _app = app
    End Function

    Public Async Function StopAsync() As Task
        Dim app = _app
        _app = Nothing
        If app Is Nothing Then Return
        For Each l In _listeners.Values
            l.Outbox.Writer.TryComplete()
        Next
        Await app.StopAsync()
        Await app.DisposeAsync()
    End Function

    ''' <summary>Sends an audio chunk (see <see cref="AudioCapture.ChunkCaptured"/>) to every listening browser.</summary>
    Public Sub BroadcastAudio(chunk As Byte())
        For Each l In _listeners.Values
            If l.Listening Then l.Outbox.Writer.TryWrite(New Outgoing With {.Data = chunk})
        Next
    End Sub

    ''' <summary>Publishes the playback state JSON to all browsers, and to browsers that connect later.</summary>
    Public Sub PublishState(stateJson As String)
        Dim bytes = Encoding.UTF8.GetBytes(stateJson)
        Volatile.Write(_latestState, bytes)
        Broadcast(bytes, isText:=True)
    End Sub

    Private Sub Broadcast(data As Byte(), isText As Boolean)
        For Each l In _listeners.Values
            l.Outbox.Writer.TryWrite(New Outgoing With {.Data = data, .IsText = isText})
        Next
    End Sub

    Private Function ConfigMessage() As Byte()
        Return JsonSerializer.SerializeToUtf8Bytes(New With {
            .type = "config",
            .latencyMs = LatencyMs,
            .sampleRate = AudioCapture.SampleRate,
            .channels = AudioCapture.Channels
        })
    End Function

    Private Async Function HandleRequestAsync(context As HttpContext) As Task
        Dim file As (ContentType As String, Data As Byte()) = Nothing
        If _files.TryGetValue(context.Request.Path.Value, file) Then
            context.Response.ContentType = file.ContentType
            context.Response.Headers.CacheControl = "no-cache"
            Await context.Response.Body.WriteAsync(file.Data)
            Return
        End If

        Select Case context.Request.Path.Value
            Case "/ws"
                If Not context.WebSockets.IsWebSocketRequest Then
                    context.Response.StatusCode = StatusCodes.Status400BadRequest
                    Return
                End If
                Using socket = Await context.WebSockets.AcceptWebSocketAsync()
                    Await HandleListenerAsync(socket, context)
                End Using
            Case Else
                context.Response.StatusCode = StatusCodes.Status404NotFound
        End Select
    End Function

    Private Async Function HandleListenerAsync(socket As WebSocket, context As HttpContext) As Task
        Dim listener As New Listener(context.Connection.RemoteIpAddress?.MapToIPv4().ToString())
        _listeners(listener.Id) = listener
        RaiseEvent ListenersChanged(Me, EventArgs.Empty)

        Using cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted)
            Dim sendTask = SendLoopAsync(socket, listener, cts.Token)
            listener.Outbox.Writer.TryWrite(New Outgoing With {.Data = ConfigMessage(), .IsText = True})
            Dim state = Volatile.Read(_latestState)
            If state IsNot Nothing Then listener.Outbox.Writer.TryWrite(New Outgoing With {.Data = state, .IsText = True})

            Try
                Await ReceiveLoopAsync(socket, listener, cts.Token)
            Catch ex As Exception When TypeOf ex Is WebSocketException OrElse TypeOf ex Is OperationCanceledException
                ' Browser went away.
            Finally
                _listeners.TryRemove(listener.Id, Nothing)
                listener.Outbox.Writer.TryComplete()
                cts.Cancel()
            End Try
            Await sendTask
        End Using
        RaiseEvent ListenersChanged(Me, EventArgs.Empty)
    End Function

    Private Shared Async Function SendLoopAsync(socket As WebSocket, listener As Listener, ct As CancellationToken) As Task
        Try
            Dim reader = listener.Outbox.Reader
            While Await reader.WaitToReadAsync(ct)
                Dim item As Outgoing = Nothing
                While reader.TryRead(item)
                    Await socket.SendAsync(New ArraySegment(Of Byte)(item.Data),
                                           If(item.IsText, WebSocketMessageType.Text, WebSocketMessageType.Binary), True, ct)
                End While
            End While
        Catch ex As Exception When TypeOf ex Is WebSocketException OrElse TypeOf ex Is OperationCanceledException
            ' Browser went away; the receive loop cleans up.
        End Try
    End Function

    Private Async Function ReceiveLoopAsync(socket As WebSocket, listener As Listener, ct As CancellationToken) As Task
        Dim buffer(4095) As Byte
        Using message As New IO.MemoryStream()
            While socket.State = WebSocketState.Open
                Dim result = Await socket.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If result.MessageType = WebSocketMessageType.Close Then Exit While
                If message.Length + result.Count > 65536 Then Exit While ' Browsers only send small control messages.
                message.Write(buffer, 0, result.Count)
                If result.EndOfMessage Then
                    If result.MessageType = WebSocketMessageType.Text Then
                        HandleListenerMessage(listener, Encoding.UTF8.GetString(message.GetBuffer(), 0, CInt(message.Length)))
                    End If
                    message.SetLength(0)
                End If
            End While
        End Using
    End Function

    Private Sub HandleListenerMessage(listener As Listener, text As String)
        Try
            Using doc = JsonDocument.Parse(text)
                Dim root = doc.RootElement
                Dim value As JsonElement
                If root.ValueKind <> JsonValueKind.Object OrElse Not root.TryGetProperty("type", value) Then Return

                Select Case value.GetString()
                    Case "ping"
                        ' Clock sync: the browser estimates the host clock from (c, s) and the round trip time.
                        Dim pong = JsonSerializer.SerializeToUtf8Bytes(New With {
                            .type = "pong",
                            .c = root.GetProperty("c").GetDouble(),
                            .s = ServerClock.NowMs()
                        })
                        listener.Outbox.Writer.TryWrite(New Outgoing With {.Data = pong, .IsText = True})

                    Case "hello"
                        If root.TryGetProperty("name", value) AndAlso value.ValueKind = JsonValueKind.String Then
                            listener.Name = Left(value.GetString().Trim(), 40)
                        End If
                        If root.TryGetProperty("listening", value) Then
                            listener.Listening = value.ValueKind = JsonValueKind.True
                        End If
                        RaiseEvent ListenersChanged(Me, EventArgs.Empty)

                    Case "command"
                        Dim command As New WebCommand With {.From = If(listener.Name = "", listener.RemoteAddress, listener.Name)}
                        If root.TryGetProperty("action", value) AndAlso value.ValueKind = JsonValueKind.String Then command.Action = value.GetString()
                        If root.TryGetProperty("positionMs", value) AndAlso value.ValueKind = JsonValueKind.Number Then command.PositionMs = CLng(value.GetDouble())
                        If root.TryGetProperty("volume", value) AndAlso value.ValueKind = JsonValueKind.Number Then command.Volume = CInt(value.GetDouble())
                        If command.Action IsNot Nothing Then RaiseEvent CommandReceived(Me, command)
                End Select
            End Using
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is KeyNotFoundException
            ' Ignore malformed messages.
        End Try
    End Sub

End Class
