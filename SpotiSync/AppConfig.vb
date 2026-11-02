Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json

''' <summary>SpotiSync settings, stored in %APPDATA%\SpotiSync\config.json.</summary>
Public Class AppConfig
    Public Property SoloistExePath As String = ""
    Public Property DeviceName As String = "SpotiSync"
    ''' <summary>The Soloist API key, encrypted for the current Windows user (DPAPI).</summary>
    Public Property ApiKeyProtected As String = ""
    Public Property WebPort As Integer = 8383
    Public Property LatencyMs As Integer = 800
    ''' <summary>Empty to capture the Soloist process; otherwise the ID of an output device to loop back.</summary>
    Public Property CaptureDeviceId As String = ""
    ''' <summary>Make Soloist the active Spotify device as soon as it is logged in.</summary>
    Public Property TakeOverPlayback As Boolean = True
    ''' <summary>Silence Soloist on this PC while the rooms still hear it (see frmMain.UpdateSoloistMute).</summary>
    Public Property MuteSoloist As Boolean = True
    ''' <summary>Start Soloist as soon as SpotiSync opens (if soloist.exe and the API key are set).</summary>
    Public Property AutoStartSoloist As Boolean = True

    Private Shared ReadOnly FilePath As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotiSync", "config.json")

    Public Shared Function Load() As AppConfig
        Try
            If File.Exists(FilePath) Then
                Return If(JsonSerializer.Deserialize(Of AppConfig)(File.ReadAllText(FilePath)), New AppConfig())
            End If
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is JsonException
            ' Fall back to defaults.
        End Try
        Return New AppConfig()
    End Function

    Public Sub Save()
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath))
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Me, New JsonSerializerOptions With {.WriteIndented = True}))
    End Sub

    Public Function GetApiKey() As String
        If String.IsNullOrEmpty(ApiKeyProtected) Then Return ""
        Try
            Dim bytes = ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(bytes)
        Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is FormatException
            Return ""
        End Try
    End Function

    Public Sub SetApiKey(value As String)
        ApiKeyProtected = If(String.IsNullOrEmpty(value), "",
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Nothing, DataProtectionScope.CurrentUser)))
    End Sub
End Class
