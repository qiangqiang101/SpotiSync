Imports System.IO
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Text.Json
Imports NAudio.CoreAudioApi

''' <summary>
''' SpotiSync host: runs Spotify Soloist on this PC, captures its audio and serves a web player that
''' every room opens (http://this-pc:8383). All rooms hear the same audio in sync and share the controls.
''' </summary>
Public Class frmMain

    Private ReadOnly _hub As New WebHub()
    Private ReadOnly _deviceEnumerator As New MMDeviceEnumerator()
    Private ReadOnly _captureDeviceIds As New List(Of String)
    Private _config As AppConfig
    Private _launcher As SoloistLauncher
    Private _soloist As SoloistClient
    Private _capture As AudioCapture
    Private _webUrl As String
    Private _loading As Boolean
    Private _stoppingSoloist As Boolean

    ' Taking over playback after Soloist starts: activate it, wait until Spotify confirms, then press play.
    Private Enum TakeOverStep
        None
        WaitingForLogin
        Activating
    End Enum
    Private Const TakeOverTimeoutSeconds As Integer = 4
    Private Const TakeOverAttempts As Integer = 2
    Private _takeOver As TakeOverStep
    Private _takeOverAttempt As Integer
    Private _takeOverDeadline As DateTime

#Region "Startup and shutdown"

    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _loading = True
        Using iconStream = GetType(frmMain).Assembly.GetManifestResourceStream("SpotiSync.favicon.ico")
            Icon = New Icon(iconStream)
        End Using
        _config = AppConfig.Load()
        txtExe.Text = _config.SoloistExePath
        txtDeviceName.Text = _config.DeviceName
        txtApiKey.Text = _config.GetApiKey()
        nudPort.Value = Math.Clamp(_config.WebPort, CInt(nudPort.Minimum), CInt(nudPort.Maximum))
        nudLatency.Value = Math.Clamp(_config.LatencyMs, CInt(nudLatency.Minimum), CInt(nudLatency.Maximum))
        chkTakeOver.Checked = _config.TakeOverPlayback
        chkMuteSoloist.Checked = _config.MuteSoloist
        chkAutoStart.Checked = _config.AutoStartSoloist
        _hub.LatencyMs = CInt(nudLatency.Value)
        PopulateCaptureSources()
        _loading = False

        AddHandler _hub.CommandReceived, Sub(s, command) BeginInvoke(Sub() ExecuteCommand(command))
        AddHandler _hub.ListenersChanged, Sub(s, args) BeginInvoke(Sub() RefreshListeners())

        StartWebServer()
        RestartCapture()
        PublishState()
        tmrStatus.Start()

        If chkAutoStart.Checked AndAlso File.Exists(txtExe.Text.Trim()) AndAlso
           txtDeviceName.Text.Trim() <> "" AndAlso txtApiKey.Text.Trim() <> "" Then
            StartSoloist(interactive:=False)
        End If
    End Sub

    Private Sub frmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        tmrStatus.Stop()
        SaveConfig()
        _stoppingSoloist = True
        RestoreSoloistLevel()
        StopCapture()
        _soloist?.Dispose()
        _launcher?.Dispose()
        ' Run off the UI thread so the server's continuations can't deadlock against this wait.
        Task.Run(Function() _hub.StopAsync()).Wait(2000)
        _deviceEnumerator.Dispose()
    End Sub

    Private Sub SaveConfig()
        _config.SoloistExePath = txtExe.Text.Trim()
        _config.DeviceName = txtDeviceName.Text.Trim()
        _config.SetApiKey(txtApiKey.Text.Trim())
        _config.WebPort = CInt(nudPort.Value)
        _config.LatencyMs = CInt(nudLatency.Value)
        _config.TakeOverPlayback = chkTakeOver.Checked
        _config.MuteSoloist = chkMuteSoloist.Checked
        _config.AutoStartSoloist = chkAutoStart.Checked
        Try
            _config.Save()
        Catch ex As IOException
            Log("Could not save settings: " & ex.Message)
        End Try
    End Sub

#End Region

#Region "Soloist"

    Private Sub btnBrowse_Click(sender As Object, e As EventArgs) Handles btnBrowse.Click
        If File.Exists(txtExe.Text) Then dlgOpenExe.InitialDirectory = Path.GetDirectoryName(txtExe.Text)
        If dlgOpenExe.ShowDialog(Me) = DialogResult.OK Then txtExe.Text = dlgOpenExe.FileName
    End Sub

    Private Sub btnStart_Click(sender As Object, e As EventArgs) Handles btnStart.Click
        StartSoloist(interactive:=True)
    End Sub

    ''' <summary>Starts Soloist. When not interactive (auto-start), problems are only logged, not shown in dialogs.</summary>
    Private Async Sub StartSoloist(interactive As Boolean)
        Dim exePath = txtExe.Text.Trim()
        Dim deviceName = txtDeviceName.Text.Trim()
        Dim apiKey = txtApiKey.Text.Trim()
        If Not File.Exists(exePath) Then
            MessageBox.Show(Me, "Choose the soloist.exe you downloaded from Spotify for Developers.", "SpotiSync", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If deviceName = "" OrElse apiKey = "" Then
            MessageBox.Show(Me, "Enter a device name and your Spotify Soloist API key.", "SpotiSync", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If interactive Then SaveConfig()

        btnStart.Enabled = False
        _stoppingSoloist = False
        _launcher = New SoloistLauncher()
        AddHandler _launcher.OutputLine, Sub(s, line) Log("soloist: " & line)
        AddHandler _launcher.Exited, AddressOf OnSoloistExited
        Log("Starting Soloist…")
        Try
            Dim endpoint = Await _launcher.StartAsync(exePath, deviceName, apiKey)
            Log($"Soloist API at ws://{endpoint}")
            _soloist = New SoloistClient(endpoint)
            AddHandler _soloist.Changed, Sub(s, args) OnSoloistChanged()
            AddHandler _soloist.LogMessage, Sub(s, message) Log(message)
            _soloist.Start()
            _takeOver = If(chkTakeOver.Checked, TakeOverStep.WaitingForLogin, TakeOverStep.None)
            btnStop.Enabled = True
            RestartCapture()
        Catch ex As Exception
            Log("Could not start Soloist: " & ex.Message)
            If interactive Then MessageBox.Show(Me, ex.Message, "Could not start Soloist", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _stoppingSoloist = True
            _launcher.Dispose()
            _launcher = Nothing
            btnStart.Enabled = True
        End Try
        UpdateSoloistStatus()
    End Sub

    Private Sub btnStop_Click(sender As Object, e As EventArgs) Handles btnStop.Click
        _stoppingSoloist = True
        RestoreSoloistLevel()
        _launcher?.StopProcess()
    End Sub

    Private Sub OnSoloistExited(sender As Object, exitCode As Integer)
        If sender IsNot _launcher Then Return
        If _stoppingSoloist Then
            Log("Soloist stopped.")
        Else
            Log(SoloistLauncher.DescribeExit(exitCode))
        End If
        _soloist?.Dispose()
        _soloist = Nothing
        _launcher.Dispose()
        _launcher = Nothing
        If IsProcessCapture() Then StopCapture()
        _takeOver = TakeOverStep.None
        btnStart.Enabled = True
        btnStop.Enabled = False
        OnSoloistChanged()
    End Sub

    Private Sub OnSoloistChanged()
        AdvanceTakeOver()
        UpdateSoloistStatus()
        PublishState()
    End Sub

    ''' <summary>
    ''' Once per start: move playback from the phone/PC app to Soloist and start playing, so every room hears
    ''' it. "activate" only makes Soloist the active device, so wait for Spotify to confirm, then press play.
    ''' Called on every Soloist event and from the 1 s timer (for the timeout).
    ''' </summary>
    Private Sub AdvanceTakeOver()
        Dim c = _soloist
        If _takeOver = TakeOverStep.None OrElse c Is Nothing OrElse Not c.LoggedIn Then Return

        If c.IsActive Then
            _takeOver = TakeOverStep.None
            If c.Status <> "playing" AndAlso c.Status <> "buffering" Then
                Log("Spotify is playing on " & c.Label & ", starting playback")
                c.ResumePlayback()
            End If
            Return
        End If

        If _takeOver = TakeOverStep.WaitingForLogin OrElse DateTime.Now > _takeOverDeadline Then
            If _takeOver = TakeOverStep.Activating AndAlso _takeOverAttempt >= TakeOverAttempts Then
                _takeOver = TakeOverStep.None
                Log($"Spotify didn't switch to ""{c.Label}"". Select it in the Spotify app, or press play in a room.")
                Return
            End If
            _takeOverAttempt = If(_takeOver = TakeOverStep.WaitingForLogin, 1, _takeOverAttempt + 1)
            _takeOver = TakeOverStep.Activating
            _takeOverDeadline = DateTime.Now.AddSeconds(TakeOverTimeoutSeconds)
            Log("Switching Spotify playback to " & c.Label & If(_takeOverAttempt > 1, " (retrying)", ""))
            c.Activate()
        End If
    End Sub

#End Region

#Region "Muting Soloist on this PC"

    ' Muting soloist.exe outright would also silence the capture (it is taken after the volume-mixer level).
    ' Instead Soloist is turned down to -120 dB on this PC (inaudible) and the float capture is boosted by
    ' the same factor, which restores the rooms' audio bit-for-bit.
    Private Const MutedLevel As Single = 0.000001F
    Private Const MutedTolerance As Single = MutedLevel * 1.5F
    ' Wait between changing the volume and the boost, so they can never overlap into a loud burst.
    Private Const MuteSettleMs As Integer = 250
    Private _soloistRestoreLevel As Single = 1.0F
    Private _muteChanging As Boolean

    Private Sub chkMuteSoloist_CheckedChanged(sender As Object, e As EventArgs) Handles chkMuteSoloist.CheckedChanged
        If Not _loading Then UpdateSoloistMute()
    End Sub

    Private Function ShouldMuteSoloist() As Boolean
        ' Only with process capture: a device capture also contains other apps, which the boost would blow up.
        Return chkMuteSoloist.Checked AndAlso IsProcessCapture() AndAlso _launcher IsNot Nothing AndAlso _launcher.IsRunning
    End Function

    ''' <summary>Soloist's volume-mixer sessions. Soloist only opens one once it starts playing.</summary>
    Private Function FindSoloistSessions() As List(Of SimpleAudioVolume)
        Dim result As New List(Of SimpleAudioVolume)
        If _launcher Is Nothing OrElse Not _launcher.IsRunning Then Return result
        Dim soloistPid = _launcher.ProcessId
        Try
            For Each device In _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                Dim sessions = device.AudioSessionManager.Sessions
                For i = 0 To sessions.Count - 1
                    If CInt(sessions(i).GetProcessID) = soloistPid Then result.Add(sessions(i).SimpleAudioVolume)
                Next
            Next
        Catch ex As Exception When TypeOf ex Is Runtime.InteropServices.COMException OrElse TypeOf ex Is NotSupportedException
            ' Device changing; try again on the next tick.
        End Try
        Return result
    End Function

    ''' <summary>Brings Soloist's level and the capture boost in line with the "Mute Soloist" option.</summary>
    Private Async Sub UpdateSoloistMute()
        If _muteChanging Then Return
        Dim sessions = FindSoloistSessions()
        If sessions.Count = 0 Then
            If _capture IsNot Nothing Then _capture.Gain = 1.0
            Return
        End If

        _muteChanging = True
        Try
            Dim isMuted = sessions.All(Function(s) s.Volume <= MutedTolerance)
            If ShouldMuteSoloist() Then
                If Not isMuted Then
                    _soloistRestoreLevel = sessions.Max(Function(s) s.Volume)
                    ' Turn down first, boost afterwards: in between the rooms get a moment of silence, never a blast.
                    For Each s In sessions
                        s.Volume = MutedLevel
                    Next
                    Log("Muted Soloist on this PC (the rooms still hear it)")
                    Await Task.Delay(MuteSettleMs)
                End If
                If _capture IsNot Nothing AndAlso ShouldMuteSoloist() Then _capture.Gain = 1.0 / MutedLevel
            Else
                ' Remove the boost first, then turn Soloist back up.
                If _capture IsNot Nothing Then _capture.Gain = 1.0
                If isMuted Then
                    Await Task.Delay(MuteSettleMs)
                    RestoreSoloistLevel(sessions)
                End If
            End If
        Catch ex As Exception When TypeOf ex Is Runtime.InteropServices.COMException
            Log("Could not change Soloist's volume: " & ex.Message)
        Finally
            _muteChanging = False
        End Try
    End Sub

    ''' <summary>
    ''' Puts Soloist's volume-mixer level back. Windows remembers per-app levels, so without this Soloist
    ''' would stay silent the next time it runs outside SpotiSync.
    ''' </summary>
    Private Sub RestoreSoloistLevel(Optional sessions As List(Of SimpleAudioVolume) = Nothing)
        If _capture IsNot Nothing Then _capture.Gain = 1.0
        Try
            For Each s In If(sessions, FindSoloistSessions())
                If s.Volume <= MutedTolerance Then s.Volume = _soloistRestoreLevel
            Next
        Catch ex As Exception When TypeOf ex Is Runtime.InteropServices.COMException
            ' Soloist is already gone.
        End Try
    End Sub

#End Region

#Region "Soloist status and commands"

    Private Sub UpdateSoloistStatus()
        Dim c = _soloist
        If c Is Nothing Then
            lblSoloistStatus.Text = If(_launcher Is Nothing, "Not running", "Starting…")
        ElseIf Not c.IsConnected Then
            lblSoloistStatus.Text = "Connecting to Soloist…"
        ElseIf Not c.LoggedIn Then
            lblSoloistStatus.Text = $"Waiting for pairing: open Spotify on your phone or PC and select the device ""{txtDeviceName.Text}""."
        Else
            lblSoloistStatus.Text = $"Ready as ""{c.DeviceName}""" & If(c.IsActive, " (active device)", " (not the active device; pick it in Spotify or press play)")
        End If

        If c Is Nothing OrElse String.IsNullOrEmpty(c.TrackName) Then
            lblNowPlaying.Text = "Nothing playing"
        Else
            lblNowPlaying.Text = $"{If(c.Status = "playing", "▶", "⏸")}  {c.TrackName}" & If(String.IsNullOrEmpty(c.ArtistName), "", $" — {c.ArtistName}")
        End If
    End Sub

    ''' <summary>Runs a command from a room's web player against Soloist.</summary>
    Private Sub ExecuteCommand(command As WebCommand)
        Dim c = _soloist
        If c Is Nothing OrElse Not c.LoggedIn Then Return
        Select Case command.Action
            Case "play", "toggle"
                If command.Action = "toggle" AndAlso (c.Status = "playing" OrElse c.Status = "buffering") Then
                    c.Pause()
                Else
                    ' Pressing play in a room means "play here": take playback over from other devices.
                    If Not c.IsActive Then c.Activate()
                    c.ResumePlayback()
                End If
            Case "pause" : c.Pause()
            Case "stop"
                c.Pause()
                c.Seek(0)
            Case "next" : c.SkipNext()
            Case "prev" : c.SkipPrev()
            Case "seek" : c.Seek(command.PositionMs)
            Case "volume" : c.SetVolume(command.Volume)
            Case Else : Return
        End Select
        Log($"{command.From}: {command.Action}")
    End Sub

    ''' <summary>Sends now-playing, play state and position to every room's web player.</summary>
    Private Sub PublishState()
        Dim c = _soloist
        Dim ready = c IsNot Nothing AndAlso c.IsConnected
        Dim state = New With {
            .type = "state",
            .connected = ready,
            .loggedIn = ready AndAlso c.LoggedIn,
            .deviceName = If(ready AndAlso c.DeviceName IsNot Nothing, c.DeviceName, txtDeviceName.Text),
            .status = If(ready, c.Status, "idle"),
            .title = If(ready, c.TrackName, Nothing),
            .artist = If(ready, c.ArtistName, Nothing),
            .album = If(ready, c.AlbumName, Nothing),
            .cover = If(ready, c.CoverUrl, Nothing),
            .durationMs = If(ready, c.DurationMs, 0L),
            .positionMs = If(ready, c.EstimatePositionMs(SoloistClient.NowMs()), 0.0),
            .positionAt = ServerClock.NowMs(),
            .speed = If(ready, c.Speed, 0.0),
            .volume = If(ready, c.Volume, 0)
        }
        _hub.PublishState(JsonSerializer.Serialize(state))
    End Sub

#End Region

#Region "Audio capture"

    Private Sub PopulateCaptureSources()
        cboSource.Items.Clear()
        _captureDeviceIds.Clear()
        cboSource.Items.Add("Soloist app only (recommended)")
        _captureDeviceIds.Add("")
        For Each device In _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            cboSource.Items.Add("Everything playing on: " & device.FriendlyName)
            _captureDeviceIds.Add(device.ID)
        Next
        cboSource.SelectedIndex = Math.Max(0, _captureDeviceIds.IndexOf(_config.CaptureDeviceId))
    End Sub

    Private Function IsProcessCapture() As Boolean
        Return cboSource.SelectedIndex <= 0
    End Function

    Private Sub cboSource_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboSource.SelectedIndexChanged
        If _loading Then Return
        _config.CaptureDeviceId = _captureDeviceIds(cboSource.SelectedIndex)
        RestartCapture()
    End Sub

    Private Async Sub RestartCapture()
        StopCapture()
        Try
            Dim capture As AudioCapture
            If IsProcessCapture() Then
                If _launcher Is Nothing OrElse Not _launcher.IsRunning Then Return
                capture = Await AudioCapture.ForProcessAsync(_launcher.ProcessId)
            Else
                capture = AudioCapture.ForDevice(_deviceEnumerator.GetDevice(_captureDeviceIds(cboSource.SelectedIndex)))
            End If
            AddHandler capture.ChunkCaptured, Sub(s, chunk) _hub.BroadcastAudio(chunk)
            AddHandler capture.Failed, Sub(s, message) BeginInvoke(Sub() Log("Audio capture stopped: " & message))
            StopCapture()   ' In case another restart finished while we were awaiting.
            _capture = capture
            capture.Start()
            Log("Capturing audio from " & cboSource.Text)
            UpdateSoloistMute()
        Catch ex As Exception
            Log("Could not capture audio: " & ex.Message)
        End Try
    End Sub

    Private Sub StopCapture()
        _capture?.Dispose()
        _capture = Nothing
    End Sub

#End Region

#Region "Web server"

    Private Async Sub StartWebServer()
        Dim port = CInt(nudPort.Value)
        btnRestartWeb.Enabled = False
        Try
            Await _hub.StopAsync()
            Await _hub.StartAsync(port)
            Dim addresses = GetLanAddresses()
            _webUrl = $"http://{If(addresses.FirstOrDefault(), "localhost")}:{port}"
            lnkUrl.Text = _webUrl
            Log("Web player: " & String.Join("  ", addresses.Select(Function(a) $"http://{a}:{port}")))
            Log("Open it in a browser in each room. If other PCs can't connect, allow SpotiSync through Windows Firewall on Private networks.")
        Catch ex As Exception
            _webUrl = Nothing
            lnkUrl.Text = "Web server not running"
            Log($"Could not start the web server on port {port}: {ex.Message}")
        End Try
        btnRestartWeb.Enabled = True
    End Sub

    Private Sub btnRestartWeb_Click(sender As Object, e As EventArgs) Handles btnRestartWeb.Click
        StartWebServer()
    End Sub

    Private Sub lnkUrl_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkUrl.LinkClicked
        If _webUrl IsNot Nothing Then Process.Start(New ProcessStartInfo(_webUrl) With {.UseShellExecute = True})
    End Sub

    Private Sub nudLatency_ValueChanged(sender As Object, e As EventArgs) Handles nudLatency.ValueChanged
        If _loading Then Return
        _hub.LatencyMs = CInt(nudLatency.Value)
        PublishState()
    End Sub

    Private Sub RefreshListeners()
        lvListeners.BeginUpdate()
        lvListeners.Items.Clear()
        For Each listener In _hub.Listeners
            Dim item = lvListeners.Items.Add(If(listener.Name = "", "(unnamed)", listener.Name))
            item.SubItems.Add(listener.RemoteAddress)
            item.SubItems.Add(If(listener.Listening, "Listening", "Controls only"))
            item.SubItems.Add(listener.ConnectedAt.ToString("g"))
        Next
        lvListeners.EndUpdate()
    End Sub

    ''' <summary>This PC's IPv4 addresses on the home network, best first (wired/Wi-Fi before VPNs).</summary>
    Private Shared Function GetLanAddresses() As List(Of String)
        Dim virtualHints = {"virtual", "vpn", "radmin", "zerotier", "hamachi", "tailscale", "wireguard", "tap-", "tun", "hyper-v", "vmware", "virtualbox", "wsl", "loopback"}
        Dim candidates As New List(Of (Address As String, Rank As Integer))
        For Each nic In NetworkInterface.GetAllNetworkInterfaces()
            If nic.OperationalStatus <> OperationalStatus.Up OrElse nic.NetworkInterfaceType = NetworkInterfaceType.Loopback Then Continue For
            Dim properties = nic.GetIPProperties()
            If properties.GatewayAddresses.Count = 0 Then Continue For
            Dim description = (nic.Name & " " & nic.Description).ToLowerInvariant()
            Dim isVirtual = virtualHints.Any(Function(hint) description.Contains(hint))
            Dim isPhysical = nic.NetworkInterfaceType = NetworkInterfaceType.Ethernet OrElse nic.NetworkInterfaceType = NetworkInterfaceType.Wireless80211
            For Each address In properties.UnicastAddresses
                If address.Address.AddressFamily <> AddressFamily.InterNetwork Then Continue For
                ' Rank: physical adapter with a home-network (private) address first; VPN adapters last.
                Dim rank = If(isVirtual, 2, 0) + If(isPhysical, 0, 1) + If(IsPrivateAddress(address.Address), 0, 1)
                candidates.Add((address.Address.ToString(), rank))
            Next
        Next
        Return candidates.OrderBy(Function(c) c.Rank).Select(Function(c) c.Address).ToList()
    End Function

    Private Shared Function IsPrivateAddress(address As Net.IPAddress) As Boolean
        Dim b = address.GetAddressBytes()
        Return b(0) = 10 OrElse (b(0) = 172 AndAlso b(1) >= 16 AndAlso b(1) <= 31) OrElse (b(0) = 192 AndAlso b(1) = 168)
    End Function

#End Region

    Private Sub tmrStatus_Tick(sender As Object, e As EventArgs) Handles tmrStatus.Tick
        ' Refresh the position anchor regularly so web players don't drift from Soloist.
        PublishState()
        ' Soloist opens its audio session only once it starts playing, so keep checking the mute.
        UpdateSoloistMute()
        AdvanceTakeOver()
        Dim listening = _hub.Listeners.Count(Function(l) l.Listening)
        If _capture Is Nothing Then
            lblAudioStatus.Text = If(IsProcessCapture(), "Not capturing (starts with Soloist)", "Not capturing")
        ElseIf _capture.HasSignal Then
            lblAudioStatus.Text = $"Streaming 48 kHz stereo to {listening} room(s)"
        Else
            lblAudioStatus.Text = $"Capturing, no sound right now · {listening} room(s) listening"
        End If
    End Sub

    Private Sub Log(message As String)
        If txtLog.TextLength > 60000 Then txtLog.Text = txtLog.Text.Substring(30000)
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}")
    End Sub

End Class
