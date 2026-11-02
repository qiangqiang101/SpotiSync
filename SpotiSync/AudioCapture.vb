Imports System.Runtime.InteropServices
Imports System.Threading
Imports NAudio.CoreAudioApi
Imports NAudio.Wave

''' <summary>Monotonic host clock shared by audio timestamps and browser clock sync (QPC based, in ms).</summary>
Public Module ServerClock
    Public Function NowMs() As Double
        Return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency
    End Function
End Module

''' <summary>
''' Captures audio with WASAPI loopback, either from one process tree (the Soloist app, whatever output it
''' plays to) or from a whole output device, as 48 kHz 16-bit stereo cut into 20 ms timestamped chunks.
''' Capture is 32-bit float, so a <see cref="Gain"/> can undo a very low app volume without losing quality.
''' </summary>
Public Class AudioCapture
    Implements IDisposable

    Public Const SampleRate As Integer = 48000
    Public Const Channels As Integer = 2
    Public Const ChunkFrames As Integer = SampleRate \ 50
    Private Const OutputBytesPerFrame As Integer = Channels * 2
    Private Const HeaderBytes As Integer = 8
    ''' <summary>Packets that would exceed this level after the gain are dropped (gain/volume change in flight).</summary>
    Private Const MaxBoostedPeak As Double = 2.0

    Private ReadOnly _client As AudioClient
    Private _thread As Thread
    Private _running As Boolean
    Private _disposed As Boolean
    Private _gain As Double = 1.0
    Private _lastChunkAt As Double = Double.NegativeInfinity

    ''' <summary>
    ''' A captured chunk: 8-byte little-endian double (host clock ms of the first frame) followed by
    ''' interleaved 16-bit PCM. Raised on the capture thread; the array is not reused.
    ''' </summary>
    Public Event ChunkCaptured As EventHandler(Of Byte())
    ''' <summary>Capture stopped because of an error. Raised on the capture thread.</summary>
    Public Event Failed As EventHandler(Of String)

    Public Shared Async Function ForProcessAsync(processId As Integer) As Task(Of AudioCapture)
        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) Then
            Throw New PlatformNotSupportedException("Capturing just the Soloist app needs Windows 10 version 2004 or later. Choose an output device to capture instead.")
        End If
        Dim client =Await AudioClient.ActivateProcessLoopbackAsync(CUInt(processId), ProcessLoopbackMode.IncludeTargetProcessTree)
        Return New AudioCapture(client)
    End Function

    Public Shared Function ForDevice(device As MMDevice) As AudioCapture
        Return New AudioCapture(device.CreateAudioClient())
    End Function

    Private Sub New(client As AudioClient)
        _client = client
        _client.Initialize(AudioClientShareMode.Shared,
                           AudioClientStreamFlags.Loopback Or AudioClientStreamFlags.AutoConvertPcm Or AudioClientStreamFlags.SrcDefaultQuality,
                           2_000_000, 0, WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels), Guid.Empty)
    End Sub

    ''' <summary>
    ''' Linear gain applied to captured audio. Process capture happens after the app's volume-mixer level, so
    ''' with Soloist turned down to a tiny level on this PC, a gain of 1/level restores it exactly for the rooms.
    ''' </summary>
    Public Property Gain As Double
        Get
            Return Volatile.Read(_gain)
        End Get
        Set(value As Double)
            Volatile.Write(_gain, value)
        End Set
    End Property

    ''' <summary>True if non-silent audio was captured in the last two seconds.</summary>
    Public ReadOnly Property HasSignal As Boolean
        Get
            Return ServerClock.NowMs() - Volatile.Read(_lastChunkAt) < 2000
        End Get
    End Property

    Public Sub Start()
        _running = True
        _client.Start()
        _thread = New Thread(AddressOf CaptureLoop) With {.IsBackground = True, .Name = "SpotiSync capture", .Priority = ThreadPriority.AboveNormal}
        _thread.Start()
    End Sub

    Private Sub CaptureLoop()
        Dim capture = _client.AudioCaptureClient
        Dim samples As Single() = Array.Empty(Of Single)()
        Dim chunk As Byte() = Nothing
        Dim filled As Integer = 0
        Dim chunkSilent As Boolean = True
        Dim chunkStartMs As Double
        Dim expectedMs As Double = Double.NaN

        Try
            While Volatile.Read(_running)
                Thread.Sleep(5)
                While capture.GetNextPacketSize() > 0
                    Dim frames As Integer
                    Dim flags As AudioClientBufferFlags
                    Dim devicePosition, qpcPosition As Long
                    Dim data = capture.GetBuffer(frames, flags, devicePosition, qpcPosition)
                    Dim nowMs = ServerClock.NowMs()

                    ' WASAPI reports the capture time of the first frame in 100 ns QPC units, the same
                    ' timebase as ServerClock. Fall back to arrival time if it is missing or implausible.
                    Dim packetMs = qpcPosition / 10000.0
                    Dim timestampValid = qpcPosition <> 0 AndAlso Math.Abs(nowMs - packetMs) < 1000
                    If Not timestampValid Then
                        packetMs = nowMs - frames * 1000.0 / SampleRate
                        ' Arrival times jitter; follow them smoothly unless there is a real gap.
                        If Not Double.IsNaN(expectedMs) AndAlso Math.Abs(packetMs - expectedMs) < 50 Then
                            packetMs = expectedMs + (packetMs - expectedMs) * 0.05
                        End If
                    End If

                    ' A gap or jump (e.g. Soloist paused): flush what we have, start a fresh chunk.
                    Dim discontinuous = Double.IsNaN(expectedMs) OrElse Math.Abs(packetMs - expectedMs) > 5 OrElse
                                        flags.HasFlag(AudioClientBufferFlags.DataDiscontinuity)
                    If discontinuous AndAlso filled > 0 Then
                        Emit(chunk, filled, chunkStartMs, chunkSilent)
                        filled = 0
                    End If

                    Dim sampleCount = frames * Channels
                    If samples.Length < sampleCount Then samples = New Single(sampleCount - 1) {}
                    Dim silent = flags.HasFlag(AudioClientBufferFlags.Silent)
                    Dim boost = Gain
                    If Not silent Then
                        Marshal.Copy(data, samples, 0, sampleCount)
                        ' Safety: if the gain and the app volume are briefly out of step, the boosted audio would
                        ' be a full-scale blast in every room. Drop such packets instead.
                        Dim peak As Single = 0
                        For i = 0 To sampleCount - 1
                            peak = Math.Max(peak, Math.Abs(samples(i)))
                        Next
                        If peak * boost > MaxBoostedPeak Then silent = True
                    End If

                    Dim offset = 0
                    While offset < frames
                        If filled = 0 Then
                            chunk = New Byte(HeaderBytes + ChunkFrames * OutputBytesPerFrame - 1) {}
                            chunkStartMs = packetMs + offset * 1000.0 / SampleRate
                            chunkSilent = True
                        End If
                        Dim count = Math.Min(frames - offset, ChunkFrames - filled)
                        If Not silent Then
                            Dim position = HeaderBytes + filled * OutputBytesPerFrame
                            For i = offset * Channels To (offset + count) * Channels - 1
                                Dim value = CInt(Math.Round(Math.Clamp(samples(i) * boost, -1.0, 1.0) * 32767))
                                If value <> 0 Then chunkSilent = False
                                chunk(position) = CByte(value And &HFF)
                                chunk(position + 1) = CByte((value >> 8) And &HFF)
                                position += 2
                            Next
                        End If
                        filled += count
                        offset += count
                        If filled = ChunkFrames Then
                            Emit(chunk, filled, chunkStartMs, chunkSilent)
                            filled = 0
                        End If
                    End While

                    expectedMs = packetMs + frames * 1000.0 / SampleRate
                    capture.ReleaseBuffer(frames)
                End While
            End While
        Catch ex As Exception
            If Volatile.Read(_running) Then RaiseEvent Failed(Me, ex.Message)
        End Try
    End Sub

    Private Sub Emit(chunk As Byte(), frames As Integer, startMs As Double, silent As Boolean)
        ' Don't stream silence: browsers simply have nothing to play while Soloist is paused.
        If silent Then Return
        Buffer.BlockCopy(BitConverter.GetBytes(startMs), 0, chunk, 0, HeaderBytes)
        If frames < ChunkFrames Then Array.Resize(chunk, HeaderBytes + frames * OutputBytesPerFrame)
        Volatile.Write(_lastChunkAt, ServerClock.NowMs())
        RaiseEvent ChunkCaptured(Me, chunk)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Volatile.Write(_running, False)
        _thread?.Join(500)
        Try
            _client.Stop()
        Catch ex As COMException
            ' Never started, or the captured process is already gone.
        End Try
        _client.Dispose()
    End Sub

End Class
