<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        grpSoloist = New GroupBox()
        lblExe = New Label()
        txtExe = New TextBox()
        btnBrowse = New Button()
        lblDeviceName = New Label()
        txtDeviceName = New TextBox()
        lblApiKey = New Label()
        txtApiKey = New TextBox()
        btnStart = New Button()
        btnStop = New Button()
        lblSoloistStatus = New Label()
        chkTakeOver = New CheckBox()
        chkMuteSoloist = New CheckBox()
        chkAutoStart = New CheckBox()
        grpAudio = New GroupBox()
        lblSource = New Label()
        cboSource = New ComboBox()
        lblLatency = New Label()
        nudLatency = New NumericUpDown()
        lblAudioStatus = New Label()
        lblAudioHint = New Label()
        grpWeb = New GroupBox()
        lblPort = New Label()
        nudPort = New NumericUpDown()
        btnRestartWeb = New Button()
        lnkUrl = New LinkLabel()
        lvListeners = New ListView()
        colRoom = New ColumnHeader()
        colAddress = New ColumnHeader()
        colAudio = New ColumnHeader()
        colConnected = New ColumnHeader()
        lblNowPlaying = New Label()
        txtLog = New TextBox()
        tmrStatus = New Timer(components)
        dlgOpenExe = New OpenFileDialog()
        grpSoloist.SuspendLayout()
        grpAudio.SuspendLayout()
        CType(nudLatency, System.ComponentModel.ISupportInitialize).BeginInit()
        grpWeb.SuspendLayout()
        CType(nudPort, System.ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        '
        'grpSoloist
        '
        grpSoloist.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        grpSoloist.Controls.Add(lblExe)
        grpSoloist.Controls.Add(txtExe)
        grpSoloist.Controls.Add(btnBrowse)
        grpSoloist.Controls.Add(lblDeviceName)
        grpSoloist.Controls.Add(txtDeviceName)
        grpSoloist.Controls.Add(lblApiKey)
        grpSoloist.Controls.Add(txtApiKey)
        grpSoloist.Controls.Add(btnStart)
        grpSoloist.Controls.Add(btnStop)
        grpSoloist.Controls.Add(lblSoloistStatus)
        grpSoloist.Controls.Add(chkTakeOver)
        grpSoloist.Controls.Add(chkMuteSoloist)
        grpSoloist.Controls.Add(chkAutoStart)
        grpSoloist.Location = New Point(12, 12)
        grpSoloist.Name = "grpSoloist"
        grpSoloist.Size = New Size(736, 176)
        grpSoloist.TabIndex = 0
        grpSoloist.TabStop = False
        grpSoloist.Text = "Spotify Soloist"
        '
        'lblExe
        '
        lblExe.AutoSize = True
        lblExe.Location = New Point(12, 27)
        lblExe.Name = "lblExe"
        lblExe.Size = New Size(66, 15)
        lblExe.TabIndex = 0
        lblExe.Text = "soloist.exe:"
        '
        'txtExe
        '
        txtExe.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtExe.Location = New Point(110, 24)
        txtExe.Name = "txtExe"
        txtExe.Size = New Size(520, 23)
        txtExe.TabIndex = 1
        '
        'btnBrowse
        '
        btnBrowse.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnBrowse.Location = New Point(640, 23)
        btnBrowse.Name = "btnBrowse"
        btnBrowse.Size = New Size(84, 25)
        btnBrowse.TabIndex = 2
        btnBrowse.Text = "Browse…"
        btnBrowse.UseVisualStyleBackColor = True
        '
        'lblDeviceName
        '
        lblDeviceName.AutoSize = True
        lblDeviceName.Location = New Point(12, 59)
        lblDeviceName.Name = "lblDeviceName"
        lblDeviceName.Size = New Size(77, 15)
        lblDeviceName.TabIndex = 3
        lblDeviceName.Text = "Device name:"
        '
        'txtDeviceName
        '
        txtDeviceName.Location = New Point(110, 56)
        txtDeviceName.Name = "txtDeviceName"
        txtDeviceName.Size = New Size(200, 23)
        txtDeviceName.TabIndex = 4
        '
        'lblApiKey
        '
        lblApiKey.AutoSize = True
        lblApiKey.Location = New Point(326, 59)
        lblApiKey.Name = "lblApiKey"
        lblApiKey.Size = New Size(50, 15)
        lblApiKey.TabIndex = 5
        lblApiKey.Text = "API key:"
        '
        'txtApiKey
        '
        txtApiKey.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtApiKey.Location = New Point(384, 56)
        txtApiKey.Name = "txtApiKey"
        txtApiKey.Size = New Size(246, 23)
        txtApiKey.TabIndex = 6
        txtApiKey.UseSystemPasswordChar = True
        '
        'btnStart
        '
        btnStart.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnStart.Location = New Point(640, 55)
        btnStart.Name = "btnStart"
        btnStart.Size = New Size(84, 25)
        btnStart.TabIndex = 7
        btnStart.Text = "Start"
        btnStart.UseVisualStyleBackColor = True
        '
        'btnStop
        '
        btnStop.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnStop.Enabled = False
        btnStop.Location = New Point(640, 86)
        btnStop.Name = "btnStop"
        btnStop.Size = New Size(84, 25)
        btnStop.TabIndex = 8
        btnStop.Text = "Stop"
        btnStop.UseVisualStyleBackColor = True
        '
        'lblSoloistStatus
        '
        lblSoloistStatus.AutoSize = True
        lblSoloistStatus.Location = New Point(12, 91)
        lblSoloistStatus.Name = "lblSoloistStatus"
        lblSoloistStatus.Size = New Size(72, 15)
        lblSoloistStatus.TabIndex = 9
        lblSoloistStatus.Text = "Not running"
        '
        'chkTakeOver
        '
        chkTakeOver.AutoSize = True
        chkTakeOver.Checked = True
        chkTakeOver.CheckState = CheckState.Checked
        chkTakeOver.Location = New Point(12, 118)
        chkTakeOver.Name = "chkTakeOver"
        chkTakeOver.Size = New Size(270, 19)
        chkTakeOver.TabIndex = 10
        chkTakeOver.Text = "Switch Spotify to SpotiSync and play on start"
        chkTakeOver.UseVisualStyleBackColor = True
        '
        'chkMuteSoloist
        '
        chkMuteSoloist.AutoSize = True
        chkMuteSoloist.Checked = True
        chkMuteSoloist.CheckState = CheckState.Checked
        chkMuteSoloist.Location = New Point(326, 118)
        chkMuteSoloist.Name = "chkMuteSoloist"
        chkMuteSoloist.Size = New Size(330, 19)
        chkMuteSoloist.TabIndex = 11
        chkMuteSoloist.Text = "Mute Soloist on this PC (rooms still hear it)"
        chkMuteSoloist.UseVisualStyleBackColor = True
        '
        'chkAutoStart
        '
        chkAutoStart.AutoSize = True
        chkAutoStart.Checked = True
        chkAutoStart.CheckState = CheckState.Checked
        chkAutoStart.Location = New Point(12, 144)
        chkAutoStart.Name = "chkAutoStart"
        chkAutoStart.Size = New Size(260, 19)
        chkAutoStart.TabIndex = 12
        chkAutoStart.Text = "Start Soloist automatically when SpotiSync opens"
        chkAutoStart.UseVisualStyleBackColor = True
        '
        'grpAudio
        '
        grpAudio.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        grpAudio.Controls.Add(lblSource)
        grpAudio.Controls.Add(cboSource)
        grpAudio.Controls.Add(lblLatency)
        grpAudio.Controls.Add(nudLatency)
        grpAudio.Controls.Add(lblAudioStatus)
        grpAudio.Controls.Add(lblAudioHint)
        grpAudio.Location = New Point(12, 194)
        grpAudio.Name = "grpAudio"
        grpAudio.Size = New Size(736, 104)
        grpAudio.TabIndex = 1
        grpAudio.TabStop = False
        grpAudio.Text = "Audio"
        '
        'lblSource
        '
        lblSource.AutoSize = True
        lblSource.Location = New Point(12, 27)
        lblSource.Name = "lblSource"
        lblSource.Size = New Size(52, 15)
        lblSource.TabIndex = 0
        lblSource.Text = "Capture:"
        '
        'cboSource
        '
        cboSource.DropDownStyle = ComboBoxStyle.DropDownList
        cboSource.FormattingEnabled = True
        cboSource.Location = New Point(110, 24)
        cboSource.Name = "cboSource"
        cboSource.Size = New Size(380, 23)
        cboSource.TabIndex = 1
        '
        'lblLatency
        '
        lblLatency.AutoSize = True
        lblLatency.Location = New Point(510, 27)
        lblLatency.Name = "lblLatency"
        lblLatency.Size = New Size(108, 15)
        lblLatency.TabIndex = 2
        lblLatency.Text = "Room buffer (ms):"
        '
        'nudLatency
        '
        nudLatency.Increment = New Decimal(New Integer() {100, 0, 0, 0})
        nudLatency.Location = New Point(640, 24)
        nudLatency.Maximum = New Decimal(New Integer() {5000, 0, 0, 0})
        nudLatency.Minimum = New Decimal(New Integer() {200, 0, 0, 0})
        nudLatency.Name = "nudLatency"
        nudLatency.Size = New Size(84, 23)
        nudLatency.TabIndex = 3
        nudLatency.Value = New Decimal(New Integer() {800, 0, 0, 0})
        '
        'lblAudioStatus
        '
        lblAudioStatus.AutoSize = True
        lblAudioStatus.Location = New Point(12, 55)
        lblAudioStatus.Name = "lblAudioStatus"
        lblAudioStatus.Size = New Size(76, 15)
        lblAudioStatus.TabIndex = 4
        lblAudioStatus.Text = "Not capturing"
        '
        'lblAudioHint
        '
        lblAudioHint.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lblAudioHint.ForeColor = SystemColors.GrayText
        lblAudioHint.Location = New Point(12, 74)
        lblAudioHint.Name = "lblAudioHint"
        lblAudioHint.Size = New Size(712, 24)
        lblAudioHint.TabIndex = 5
        lblAudioHint.Text = "To hear the music on this PC too, open the web player here: it plays in sync with the other rooms. Don't mute soloist.exe in the volume mixer yourself; use the option above."
        '
        'grpWeb
        '
        grpWeb.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        grpWeb.Controls.Add(lblPort)
        grpWeb.Controls.Add(nudPort)
        grpWeb.Controls.Add(btnRestartWeb)
        grpWeb.Controls.Add(lnkUrl)
        grpWeb.Controls.Add(lvListeners)
        grpWeb.Location = New Point(12, 304)
        grpWeb.Name = "grpWeb"
        grpWeb.Size = New Size(736, 170)
        grpWeb.TabIndex = 2
        grpWeb.TabStop = False
        grpWeb.Text = "Rooms (web player)"
        '
        'lblPort
        '
        lblPort.AutoSize = True
        lblPort.Location = New Point(12, 27)
        lblPort.Name = "lblPort"
        lblPort.Size = New Size(32, 15)
        lblPort.TabIndex = 0
        lblPort.Text = "Port:"
        '
        'nudPort
        '
        nudPort.Location = New Point(110, 24)
        nudPort.Maximum = New Decimal(New Integer() {65535, 0, 0, 0})
        nudPort.Minimum = New Decimal(New Integer() {1024, 0, 0, 0})
        nudPort.Name = "nudPort"
        nudPort.Size = New Size(80, 23)
        nudPort.TabIndex = 1
        nudPort.Value = New Decimal(New Integer() {8383, 0, 0, 0})
        '
        'btnRestartWeb
        '
        btnRestartWeb.Location = New Point(196, 23)
        btnRestartWeb.Name = "btnRestartWeb"
        btnRestartWeb.Size = New Size(84, 25)
        btnRestartWeb.TabIndex = 2
        btnRestartWeb.Text = "Apply"
        btnRestartWeb.UseVisualStyleBackColor = True
        '
        'lnkUrl
        '
        lnkUrl.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lnkUrl.AutoEllipsis = True
        lnkUrl.Location = New Point(296, 27)
        lnkUrl.Name = "lnkUrl"
        lnkUrl.Size = New Size(428, 18)
        lnkUrl.TabIndex = 3
        lnkUrl.TabStop = True
        lnkUrl.Text = "Web server not running"
        '
        'lvListeners
        '
        lvListeners.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lvListeners.Columns.AddRange(New ColumnHeader() {colRoom, colAddress, colAudio, colConnected})
        lvListeners.FullRowSelect = True
        lvListeners.Location = New Point(12, 56)
        lvListeners.Name = "lvListeners"
        lvListeners.Size = New Size(712, 104)
        lvListeners.TabIndex = 4
        lvListeners.UseCompatibleStateImageBehavior = False
        lvListeners.View = View.Details
        '
        'colRoom
        '
        colRoom.Text = "Room"
        colRoom.Width = 220
        '
        'colAddress
        '
        colAddress.Text = "Address"
        colAddress.Width = 180
        '
        'colAudio
        '
        colAudio.Text = "Audio"
        colAudio.Width = 120
        '
        'colConnected
        '
        colConnected.Text = "Connected since"
        colConnected.Width = 160
        '
        'lblNowPlaying
        '
        lblNowPlaying.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lblNowPlaying.AutoEllipsis = True
        lblNowPlaying.Font = New Font("Segoe UI", 11.0F, FontStyle.Bold)
        lblNowPlaying.Location = New Point(12, 482)
        lblNowPlaying.Name = "lblNowPlaying"
        lblNowPlaying.Size = New Size(736, 24)
        lblNowPlaying.TabIndex = 3
        lblNowPlaying.Text = "Nothing playing"
        '
        'txtLog
        '
        txtLog.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        txtLog.Location = New Point(12, 512)
        txtLog.Multiline = True
        txtLog.Name = "txtLog"
        txtLog.ReadOnly = True
        txtLog.ScrollBars = ScrollBars.Vertical
        txtLog.Size = New Size(736, 150)
        txtLog.TabIndex = 4
        '
        'tmrStatus
        '
        tmrStatus.Interval = 1000
        '
        'dlgOpenExe
        '
        dlgOpenExe.Filter = "Spotify Soloist|soloist*.exe|Programs|*.exe"
        dlgOpenExe.Title = "Find soloist.exe"
        '
        'frmMain
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(760, 674)
        Controls.Add(grpSoloist)
        Controls.Add(grpAudio)
        Controls.Add(grpWeb)
        Controls.Add(lblNowPlaying)
        Controls.Add(txtLog)
        MinimumSize = New Size(700, 614)
        Name = "frmMain"
        StartPosition = FormStartPosition.CenterScreen
        Text = "SpotiSync"
        grpSoloist.ResumeLayout(False)
        grpSoloist.PerformLayout()
        grpAudio.ResumeLayout(False)
        grpAudio.PerformLayout()
        CType(nudLatency, System.ComponentModel.ISupportInitialize).EndInit()
        grpWeb.ResumeLayout(False)
        grpWeb.PerformLayout()
        CType(nudPort, System.ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents grpSoloist As GroupBox
    Friend WithEvents lblExe As Label
    Friend WithEvents txtExe As TextBox
    Friend WithEvents btnBrowse As Button
    Friend WithEvents lblDeviceName As Label
    Friend WithEvents txtDeviceName As TextBox
    Friend WithEvents lblApiKey As Label
    Friend WithEvents txtApiKey As TextBox
    Friend WithEvents btnStart As Button
    Friend WithEvents btnStop As Button
    Friend WithEvents lblSoloistStatus As Label
    Friend WithEvents chkTakeOver As CheckBox
    Friend WithEvents chkMuteSoloist As CheckBox
    Friend WithEvents chkAutoStart As CheckBox
    Friend WithEvents grpAudio As GroupBox
    Friend WithEvents lblSource As Label
    Friend WithEvents cboSource As ComboBox
    Friend WithEvents lblLatency As Label
    Friend WithEvents nudLatency As NumericUpDown
    Friend WithEvents lblAudioStatus As Label
    Friend WithEvents lblAudioHint As Label
    Friend WithEvents grpWeb As GroupBox
    Friend WithEvents lblPort As Label
    Friend WithEvents nudPort As NumericUpDown
    Friend WithEvents btnRestartWeb As Button
    Friend WithEvents lnkUrl As LinkLabel
    Friend WithEvents lvListeners As ListView
    Friend WithEvents colRoom As ColumnHeader
    Friend WithEvents colAddress As ColumnHeader
    Friend WithEvents colAudio As ColumnHeader
    Friend WithEvents colConnected As ColumnHeader
    Friend WithEvents lblNowPlaying As Label
    Friend WithEvents txtLog As TextBox
    Friend WithEvents tmrStatus As Timer
    Friend WithEvents dlgOpenExe As OpenFileDialog

End Class
