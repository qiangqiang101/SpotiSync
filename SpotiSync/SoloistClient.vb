Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading

''' <summary>
''' A connection to one Spotify Soloist instance's local WebSocket API (soloist --ws ADDR:PORT).
''' Reconnects automatically. All events are raised on the thread that created the client (the UI thread).
''' </summary>
Public Class SoloistClient
    Implements IDisposable

    Private Const ClockSampleWindow As Integer = 8

    Private ReadOnly _ui As SynchronizationContext
    Private ReadOnly _cts As New CancellationTokenSource()
    Private ReadOnly _sendLock As New SemaphoreSlim(1, 1)
    Private _socket As ClientWebSocket
    Private _disposed As Boolean
    Private _lastError As String

    ' Position anchor as reported by Soloist: position_ms at timestamp_ms (Soloist host's wall clock).
    Private _anchorPosMs As Long
    Private _anchorServerMs As Long
    Private _speed As Double
    ' Samples of (local clock - Soloist clock), taken from position_sync events. The minimum is the best estimate.
    Private ReadOnly _clockSamples As New Queue(Of Long)

    Public Event Changed As EventHandler
    Public Event LogMessage As EventHandler(Of String)

    Public Sub New(address As String)
        Me.Address = address
        _ui = SynchronizationContext.Current
    End Sub

    ''' <summary>host:port of the WebSocket API.</summary>
    Public ReadOnly Property Address As String

    Public ReadOnly Property IsConnected As Boolean
    Public ReadOnly Property LoggedIn As Boolean
    Public ReadOnly Property IsActive As Boolean
    Public ReadOnly Property DeviceName As String
    ''' <summary>idle, playing, paused or buffering.</summary>
    Public ReadOnly Property Status As String = "idle"
    Public ReadOnly Property TrackUri As String
    Public ReadOnly Property TrackName As String
    Public ReadOnly Property ArtistName As String
    Public ReadOnly Property AlbumName As String
    Public ReadOnly Property CoverUrl As String
    Public ReadOnly Property EntityType As String
    Public ReadOnly Property DurationMs As Long
    Public ReadOnly Property Volume As Integer

    Public ReadOnly Property Label As String
        Get
            Return If(String.IsNullOrEmpty(DeviceName), Address, DeviceName)
        End Get
    End Property

    ''' <summary>Playback speed multiplier; 0 when not advancing.</summary>
    Public ReadOnly Property Speed As Double
        Get
            Return _speed
        End Get
    End Property

    Public ReadOnly Property ClockOffsetMs As Long
        Get
            Return If(_clockSamples.Count = 0, 0L, _clockSamples.Min())
        End Get
    End Property

    Public Shared Function NowMs() As Long
        Return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    End Function

    ''' <summary>Estimated playback position at the given local time (Unix ms).</summary>
    Public Function EstimatePositionMs(localNowMs As Long) As Double
        Dim serverNow = localNowMs - ClockOffsetMs
        Dim pos = _anchorPosMs + (serverNow - _anchorServerMs) * _speed
        If DurationMs > 0 Then pos = Math.Min(pos, DurationMs)
        Return Math.Max(pos, 0)
    End Function

#Region "Commands"

    Public Sub Play(Optional uri As String = Nothing)
        If uri Is Nothing Then
            Send("play")
        Else
            Send("play", New Dictionary(Of String, Object) From {{"uri", uri}})
        End If
    End Sub

    Public Sub ResumePlayback()
        Send("play")
    End Sub

    Public Sub Pause()
        Send("pause")
    End Sub

    Public Sub Seek(positionMs As Long)
        Send("seek", New Dictionary(Of String, Object) From {{"position_ms", Math.Max(positionMs, 0)}})
    End Sub

    Public Sub SkipNext()
        Send("skip_next")
    End Sub

    Public Sub SkipPrev()
        Send("skip_prev")
    End Sub

    Public Sub Activate()
        Send("activate")
    End Sub

    Public Sub SetVolume(value As Integer)
        Send("set_volume", New Dictionary(Of String, Object) From {{"volume", Math.Clamp(value, 0, 100)}})
    End Sub

    Private Sub Send(command As String, Optional fields As Dictionary(Of String, Object) = Nothing)
        Dim unused = SendAsync(command, fields)
    End Sub

    Public Async Function SendAsync(command As String, Optional fields As Dictionary(Of String, Object) = Nothing) As Task
        Dim ws = _socket
        If ws Is Nothing OrElse ws.State <> WebSocketState.Open Then Return

        Dim msg As New Dictionary(Of String, Object) From {{"type", "command"}, {"command", command}}
        If fields IsNot Nothing Then
            For Each kv In fields
                msg(kv.Key) = kv.Value
            Next
        End If
        Dim bytes = JsonSerializer.SerializeToUtf8Bytes(msg)

        Dim failure As String = Nothing
        Await _sendLock.WaitAsync()
        Try
            Await ws.SendAsync(New ArraySegment(Of Byte)(bytes), WebSocketMessageType.Text, True, _cts.Token)
        Catch ex As Exception
            failure = ex.Message
        Finally
            _sendLock.Release()
        End Try
        If failure IsNot Nothing Then Post(Sub() Log($"Failed to send {command}: {failure}"))
    End Function

#End Region

#Region "Connection"

    Public Sub Start()
        Task.Run(Function() RunAsync(_cts.Token))
    End Sub

    Private Async Function RunAsync(ct As CancellationToken) As Task
        Dim uri As New Uri("ws://" & Address)
        While Not ct.IsCancellationRequested
            Using ws As New ClientWebSocket()
                Dim errorMessage As String = Nothing
                Try
                    Await ws.ConnectAsync(uri, ct)
                    _socket = ws
                    Post(Sub()
                             _IsConnected = True
                             _lastError = Nothing
                             _clockSamples.Clear()
                             Log("Connected")
                             OnChanged()
                         End Sub)
                    Await ReceiveLoopAsync(ws, ct)
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                Catch ex As Exception
                    errorMessage = ex.Message
                Finally
                    _socket = Nothing
                End Try

                Post(Sub()
                         If _IsConnected Then Log("Disconnected")
                         If errorMessage IsNot Nothing AndAlso errorMessage <> _lastError Then
                             _lastError = errorMessage
                             Log("Connection error: " & errorMessage & " (retrying)")
                         End If
                         _IsConnected = False
                         _LoggedIn = False
                         _IsActive = False
                         OnChanged()
                     End Sub)
            End Using

            Try
                Await Task.Delay(3000, ct)
            Catch ex As OperationCanceledException
                Exit While
            End Try
        End While
    End Function

    Private Async Function ReceiveLoopAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
        Dim buffer(16383) As Byte
        Using message As New IO.MemoryStream()
            While ws.State = WebSocketState.Open
                Dim result = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If result.MessageType = WebSocketMessageType.Close Then Exit While
                message.Write(buffer, 0, result.Count)
                If result.EndOfMessage Then
                    ' Timestamp on arrival, before marshalling to the UI thread, for accurate clock estimation.
                    Dim receivedAt = NowMs()
                    Dim text = Encoding.UTF8.GetString(message.GetBuffer(), 0, CInt(message.Length))
                    message.SetLength(0)
                    Post(Sub() HandleMessage(text, receivedAt))
                End If
            End While
        End Using
    End Function

#End Region

#Region "Events from Soloist"

    Private Sub HandleMessage(text As String, receivedAt As Long)
        Dim doc As JsonDocument
        Try
            doc = JsonDocument.Parse(text)
        Catch ex As JsonException
            Log("Ignoring malformed message: " & ex.Message)
            Return
        End Try

        Using doc
            Dim root = doc.RootElement
            Select Case GetStr(root, "type")
                Case "auth_state"
                    Dim wasLoggedIn = _LoggedIn
                    _LoggedIn = GetBool(root, "logged_in")
                    _IsActive = GetBool(root, "is_active")
                    _DeviceName = GetStr(root, "device_name", _DeviceName)
                    If _LoggedIn AndAlso Not wasLoggedIn Then
                        Send("get_state")
                    ElseIf Not _LoggedIn Then
                        Log("Not logged in. Select this device in the Spotify app to pair it.")
                    End If

                Case "playback_state"
                    _Status = GetStr(root, "status", "idle")
                    _IsActive = GetBool(root, "is_active", _IsActive)
                    _Volume = CInt(GetLong(root, "volume", _Volume))
                    ApplyItem(Child(root, "item"))
                    ApplyPosition(Child(root, "position"), receivedAt, sampleClock:=False)

                Case "position_sync"
                    Dim pos = Child(root, "position")
                    ApplyPosition(If(pos.ValueKind = JsonValueKind.Object, pos, root), receivedAt, sampleClock:=True)

                Case "track_changed"
                    ApplyItem(Child(root, "item"))

                Case "playback_changed"
                    _Status = GetStr(root, "status", _Status)

                Case "volume_changed"
                    _Volume = CInt(GetLong(root, "volume", _Volume))

                Case "device_changed"
                    _IsActive = GetBool(root, "is_active", _IsActive)
                    _DeviceName = GetStr(root, "device_name", _DeviceName)

                Case "error"
                    Log("Soloist error: " & GetStr(root, "message", text))

                Case Else
                    ' command_result, queue_changed, context_changed, options_changed: not needed for syncing.
                    Return
            End Select
        End Using
        OnChanged()
    End Sub

    Private Sub ApplyItem(item As JsonElement)
        If item.ValueKind <> JsonValueKind.Object Then
            _TrackUri = Nothing
            _TrackName = Nothing
            _ArtistName = Nothing
            _AlbumName = Nothing
            _CoverUrl = Nothing
            _EntityType = Nothing
            _DurationMs = 0
            Return
        End If

        _TrackUri = GetStr(item, "uri")
        _EntityType = GetStr(item, "entity_type")
        Dim decorations = Child(item, "decorations")
        _TrackName = GetStr(Child(decorations, "identity"), "name")
        _DurationMs = GetLong(Child(decorations, "playback"), "duration_ms")

        Dim names As New List(Of String)
        Dim creators = Child(decorations, "creators")
        If creators.ValueKind = JsonValueKind.Array Then
            For Each creator In creators.EnumerateArray()
                ' Creators share the entity envelope; tolerate an extra "entity" wrapper as used by "parent".
                Dim entity = Child(creator, "entity")
                If entity.ValueKind <> JsonValueKind.Object Then entity = creator
                Dim name = GetStr(Child(Child(entity, "decorations"), "identity"), "name")
                If Not String.IsNullOrEmpty(name) Then names.Add(name)
            Next
        End If
        _ArtistName = String.Join(", ", names)

        ' Album (parent) name, and cover art from the item itself or else from its album.
        Dim parent = Child(Child(decorations, "parent"), "entity")
        _AlbumName = GetStr(Child(Child(parent, "decorations"), "identity"), "name")
        _CoverUrl = If(PickCover(decorations), PickCover(Child(parent, "decorations")))
    End Sub

    Private Shared Function PickCover(decorations As JsonElement) As String
        Dim covers = Child(Child(decorations, "visual_identity"), "cover")
        If covers.ValueKind <> JsonValueKind.Array Then Return Nothing
        Dim preference = {"small", "default", "xlarge", "large"}
        Dim best As String = Nothing
        Dim bestRank = -2
        For Each cover In covers.EnumerateArray()
            Dim url = GetStr(cover, "url")
            Dim rank = Array.IndexOf(preference, GetStr(cover, "size"))
            If url IsNot Nothing AndAlso rank > bestRank Then
                best = url
                bestRank = rank
            End If
        Next
        Return best
    End Function

    Private Sub ApplyPosition(position As JsonElement, receivedAt As Long, sampleClock As Boolean)
        If position.ValueKind <> JsonValueKind.Object Then Return
        _anchorPosMs = GetLong(position, "position_ms")
        _speed = GetDouble(position, "speed", 0)
        Dim timestamp = GetLong(position, "timestamp_ms", -1)
        If timestamp < 0 Then
            _anchorServerMs = receivedAt - ClockOffsetMs
            Return
        End If
        _anchorServerMs = timestamp

        ' position_sync is sent as soon as the anchor changes, so its timestamp is close to "now" on the
        ' Soloist host. The smallest (local - remote) difference is the one with the least network delay.
        If sampleClock Then
            _clockSamples.Enqueue(receivedAt - timestamp)
            While _clockSamples.Count > ClockSampleWindow
                _clockSamples.Dequeue()
            End While
        End If
    End Sub

#End Region

#Region "Helpers"

    Private Shared Function Child(el As JsonElement, name As String) As JsonElement
        Dim value As JsonElement
        If el.ValueKind = JsonValueKind.Object AndAlso el.TryGetProperty(name, value) Then Return value
        Return Nothing
    End Function

    Private Shared Function GetStr(el As JsonElement, name As String, Optional defaultValue As String = Nothing) As String
        Dim value = Child(el, name)
        Return If(value.ValueKind = JsonValueKind.String, value.GetString(), defaultValue)
    End Function

    Private Shared Function GetDouble(el As JsonElement, name As String, Optional defaultValue As Double = 0) As Double
        Dim value = Child(el, name)
        Return If(value.ValueKind = JsonValueKind.Number, value.GetDouble(), defaultValue)
    End Function

    Private Shared Function GetLong(el As JsonElement, name As String, Optional defaultValue As Long = 0) As Long
        Dim value = Child(el, name)
        Return If(value.ValueKind = JsonValueKind.Number, CLng(value.GetDouble()), defaultValue)
    End Function

    Private Shared Function GetBool(el As JsonElement, name As String, Optional defaultValue As Boolean = False) As Boolean
        Select Case Child(el, name).ValueKind
            Case JsonValueKind.True : Return True
            Case JsonValueKind.False : Return False
            Case Else : Return defaultValue
        End Select
    End Function

    Private Sub Post(action As Action)
        If _ui Is Nothing Then
            If Not _disposed Then action()
            Return
        End If
        _ui.Post(Sub(state)
                     If Not _disposed Then action()
                 End Sub, Nothing)
    End Sub

    Private Sub Log(message As String)
        RaiseEvent LogMessage(Me, $"{Label}: {message}")
    End Sub

    Private Sub OnChanged()
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _cts.Cancel()
    End Sub

#End Region

End Class
