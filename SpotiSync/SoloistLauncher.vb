Imports System.IO
Imports System.Threading

''' <summary>
''' Starts soloist.exe on this PC with its WebSocket API on a free localhost port, and finds that port
''' through the ws.port discovery file Soloist writes to its data directory.
''' </summary>
Public Class SoloistLauncher
    Implements IDisposable

    ''' <summary>Exit code Soloist uses when its 90-day build has expired.</summary>
    Public Const ExitCodeBuildExpired As Integer = 10

    Private ReadOnly _ui As SynchronizationContext = SynchronizationContext.Current
    Private _process As Process

    ''' <summary>A line of Soloist console output. Raised on the UI thread.</summary>
    Public Event OutputLine As EventHandler(Of String)
    ''' <summary>Soloist exited; the argument is its exit code. Raised on the UI thread.</summary>
    Public Event Exited As EventHandler(Of Integer)

    Public Shared ReadOnly Property DataDirectory As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "soloist", "data")

    Public ReadOnly Property ProcessId As Integer
        Get
            Return If(_process Is Nothing, 0, _process.Id)
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _process IsNot Nothing AndAlso Not _process.HasExited
        End Get
    End Property

    ''' <summary>Starts Soloist and returns the "host:port" of its WebSocket API.</summary>
    Public Async Function StartAsync(exePath As String, deviceName As String, apiKey As String) As Task(Of String)
        If IsRunning Then Throw New InvalidOperationException("Soloist is already running.")
        If Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)).Length > 0 Then
            Throw New InvalidOperationException("Another Soloist is already running on this PC. Close it first, because SpotiSync needs to start Soloist itself.")
        End If

        Dim portFile = Path.Combine(DataDirectory, "ws.port")
        Dim launchedAt = DateTime.UtcNow.AddSeconds(-1)

        Dim psi As New ProcessStartInfo(exePath) With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .WorkingDirectory = Path.GetDirectoryName(exePath)
        }
        For Each arg In {"--device-name", deviceName, "--api-key", apiKey, "--ws", "127.0.0.1:0"}
            psi.ArgumentList.Add(arg)
        Next

        Dim p = Process.Start(psi)
        _process = p
        p.EnableRaisingEvents = True
        AddHandler p.OutputDataReceived, AddressOf OnOutput
        AddHandler p.ErrorDataReceived, AddressOf OnOutput
        AddHandler p.Exited, Sub() Post(Sub() RaiseEvent Exited(Me, p.ExitCode))
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()

        ' Wait for Soloist to publish its WebSocket port (a stale file from a crash would be older than launchedAt).
        For attempt = 1 To 100
            Await Task.Delay(200)
            If p.HasExited Then Throw New InvalidOperationException(DescribeExit(p.ExitCode))
            Dim port = TryReadPort(portFile, launchedAt)
            If port > 0 Then Return $"127.0.0.1:{port}"
        Next
        Throw New TimeoutException("Soloist started but did not open its WebSocket API (no ws.port file in " & DataDirectory & ").")
    End Function

    Public Shared Function DescribeExit(exitCode As Integer) As String
        If exitCode = ExitCodeBuildExpired Then
            Return "Soloist exited because this build has expired (builds last 90 days). Download a new build from Spotify for Developers."
        End If
        Return $"Soloist exited with code {exitCode}."
    End Function

    Private Shared Function TryReadPort(portFile As String, notBefore As DateTime) As Integer
        Try
            If Not File.Exists(portFile) OrElse File.GetLastWriteTimeUtc(portFile) < notBefore Then Return 0
            Dim text = File.ReadAllText(portFile).Trim()
            Dim port As Integer
            ' Accept "54321" as well as "127.0.0.1:54321".
            If Integer.TryParse(text.Substring(text.LastIndexOf(":"c) + 1), port) Then Return port
        Catch ex As IOException
            ' Soloist may still be writing the file; try again on the next poll.
        End Try
        Return 0
    End Function

    Public Sub StopProcess()
        Dim p = _process
        If p Is Nothing Then Return
        Try
            If Not p.HasExited Then p.Kill(entireProcessTree:=True)
        Catch ex As InvalidOperationException
            ' Already exited.
        End Try
    End Sub

    Private Sub OnOutput(sender As Object, e As DataReceivedEventArgs)
        If String.IsNullOrWhiteSpace(e.Data) Then Return
        Dim line = e.Data
        Post(Sub() RaiseEvent OutputLine(Me, line))
    End Sub

    Private Sub Post(action As Action)
        If _ui Is Nothing Then
            action()
        Else
            _ui.Post(Sub(state) action(), Nothing)
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        StopProcess()
        _process?.Dispose()
        _process = Nothing
    End Sub

End Class
