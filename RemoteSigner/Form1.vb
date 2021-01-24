Imports System.Diagnostics
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Public Class Form1
    Private Delegate Sub TextBoxSetString_TSafe(ByVal ItemText As String)
    Dim ThreadingPool As New ArrayList
    Dim ThreadingPoolMutex As New Mutex
    Dim ServerThread As Thread
    Dim ServerInstance As Socket
    Private Sub AddStringToLog(ByVal ItemText As String)
        If Me.TextBox2.InvokeRequired Then
            Dim d As New TextBoxSetString_TSafe(AddressOf AddStringToLog)
            Me.TextBox2.BeginInvoke(d, ItemText)
        Else
            TextBox2.Text += ItemText
            TextBox2.Update()
        End If
    End Sub

    Private Function GetInternetTime() As String
        If CheckBox1.Checked Then
            Dim Client As New WebClient
            Client.Encoding = Encoding.ASCII
            Dim TimeAvail As Boolean = False
            Dim TimeDetail As String = ""
            Do
                Try
                    TimeDetail = Client.DownloadString("http://worldtimeapi.org/api/ip.txt")
                    TimeAvail = True
                Catch Ex As Exception
                    TimeAvail = False
                End Try
            Loop Until TimeAvail
            Dim TimeFields() As String = Split(TimeDetail, vbLf)
            Dim s As String, t As String = ""
            For Each s In TimeFields
                If s.StartsWith("datetime: ") Then
                    t = s.Substring(10)
                End If
            Next
            Client.Dispose()
            Return t
        Else
            Return Now
        End If
    End Function

    Private Sub ServerThreadWorker(ByVal Data As Object)
        ' Initialize Incoming request
        Dim IncomingSocket As Socket = CType(Data, Socket)
        Dim EP As IPEndPoint = CType(IncomingSocket.RemoteEndPoint, IPEndPoint)
        Dim IpAddr As String = EP.Address.ToString()
        AddStringToLog(String.Format("============ Incoming Request from {0} ============" & vbCrLf, IpAddr))
        AddStringToLog("Time of Request: " & GetInternetTime() & vbCrLf)
        ' Process the Header
        Dim RequestHeader(19) As Byte       ' Header Data of Request.
        Dim RecvCounter As Integer = 0
        Do While RecvCounter < 20
            RecvCounter += IncomingSocket.Receive(RequestHeader, RecvCounter, 20 - RecvCounter, SocketFlags.None)
        Loop
        ' Compare the request signature.
        Dim Signature As String = Encoding.ASCII.GetString(RequestHeader, 0, 16)
        If Signature = "Sign Request #ZT" Then
            ' Signature match. Perform further processing.
            Dim FileSize As Integer = BitConverter.ToInt32(RequestHeader, 16)
            AddStringToLog(String.Format("Request signature match! File Size={0} bytes" & vbCrLf, FileSize))
            ' Get sign rule name.
            Dim RuleNameBuffer(255) As Byte, RuleName As String
            RecvCounter = 0      ' Reset Counter
            Do While RecvCounter < 256
                RecvCounter += IncomingSocket.Receive(RuleNameBuffer, RecvCounter, 256 - RecvCounter, SocketFlags.None)
            Loop
            RuleName = Encoding.ASCII.GetString(RuleNameBuffer, 0, RuleNameBuffer(255))
            AddStringToLog("The incoming request is signing with rule " & RuleName & vbCrLf)
            ' Receive the file
            Dim FileBuff(FileSize - 1) As Byte
            RecvCounter = 0      ' Reset Counter
            Do While RecvCounter < FileSize
                RecvCounter += IncomingSocket.Receive(FileBuff, RecvCounter, FileSize - RecvCounter, SocketFlags.None)
            Loop
            ' Write stuff into file. To ensure no conflict of file name, use GUID.
            Dim FilePath As String = AppDomain.CurrentDomain.BaseDirectory & Guid.NewGuid().ToString() & ".sys"
            AddStringToLog("The file is downloaded to " & FilePath & vbCrLf)
            Dim fs As New FileStream(FilePath, FileMode.Create)
            fs.Write(FileBuff, 0, FileSize)
            ' Close the file to let Signer gain full access.
            fs.Close()
            Dim SignerProcess As New Process()
            With SignerProcess.StartInfo
                .FileName = TextBox1.Text
                .Arguments = String.Format("Sign /r ""{0}"" /f ""{1}"" /ac", RuleName, FilePath)
                .RedirectStandardOutput = True
                .CreateNoWindow = True
                .UseShellExecute = False
            End With
            With SignerProcess
                AddHandler .OutputDataReceived, Sub(sender As Object, e As DataReceivedEventArgs)
                                                    If String.IsNullOrEmpty(e.Data) = False Then
                                                        AddStringToLog(vbCrLf & "> " & e.Data)
                                                    End If
                                                End Sub
                .Start()           ' Start signing...
                .BeginOutputReadLine()
                .WaitForExit()     ' Wait for signing...
            End With
            ' Open the file since signing is completed.
            fs = New FileStream(FilePath, FileMode.Open)
            If SignerProcess.ExitCode = 0 And fs.Length < &H7FFFFFFF Then
                ' Send back the reply header.
                Dim ReplyHeader(19) As Byte
                Array.Copy(Encoding.ASCII.GetBytes("Sign Reply #ZT-S"), 0, ReplyHeader, 0, 16)
                Array.Copy(BitConverter.GetBytes(CInt(fs.Length)), 0, ReplyHeader, 16, 4)
                Dim SendCounter As Integer = 0
                AddStringToLog(String.Format("Sending back the signed executable. File Size={0} bytes" & vbCrLf, fs.Length))
                Do While SendCounter < 20
                    SendCounter += IncomingSocket.Send(ReplyHeader, SendCounter, 20 - SendCounter, SocketFlags.None)
                Loop
                ' Read the file.
                ReDim FileBuff(fs.Length - 1)
                fs.Read(FileBuff, 0, fs.Length)
                ' Send back the signed file.
                SendCounter = 0     ' Reset counter.
                Do While SendCounter < fs.Length
                    SendCounter += IncomingSocket.Send(FileBuff, SendCounter, CInt(fs.Length) - SendCounter, SocketFlags.None)
                Loop
                AddStringToLog(String.Format("Request from {0} is processed successfully!" & vbCrLf, IpAddr))
            Else
                ' Failed to sign the file.
                ' Send back the reply header.
                Dim ReplyHeader(19) As Byte
                Array.Copy(Encoding.ASCII.GetBytes("Sign Reply #ZT-F"), 0, ReplyHeader, 0, 16)
                Array.Copy(BitConverter.GetBytes(CInt(fs.Length)), 0, ReplyHeader, 16, 4)
                Dim SendCounter As Integer = 0
                Do While SendCounter < 20
                    SendCounter += IncomingSocket.Send(ReplyHeader, SendCounter, 20 - SendCounter, SocketFlags.None)
                Loop
                AddStringToLog(String.Format("Request from {0} failed!" & vbCrLf, IpAddr))
            End If
            SignerProcess.Close()
            fs.Close()
        End If
        IncomingSocket.Close()
        AddStringToLog(String.Format("============ Closing Connection from {0} ============" & vbCrLf, IpAddr))
    End Sub

    Private Sub ServerThreadMonitor()
        ' Initialize socket...
        Dim EP As New IPEndPoint(IPAddress.Any, 1125)       ' Use 1125 as port.
        ServerInstance = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        ServerInstance.Bind(EP)
        ServerInstance.Listen(16)       ' This server is multi-threaded designed. It is unlikely to exceed the backlog limit.
        ' Start the Reaper Timer.
        ReaperTimer.Enabled = True
        Do
            ' Loop to accept incoming requests.
            Try
                Dim IncomingSocket As Socket = ServerInstance.Accept()
                ' System will block the thread to continue at Accept
                ' until there is an incoming connection.
                Dim WorkerThread As New Thread(AddressOf ServerThreadWorker)
                WorkerThread.Start(IncomingSocket)  ' Assign the incoming request to a new thread.
                ' Add the thread into threading pool.
                ' Do this in a thread-safe manner.
                ThreadingPoolMutex.WaitOne()
                ThreadingPool.Add(WorkerThread)
                ThreadingPoolMutex.ReleaseMutex()
            Catch Ex As SocketException
                ' Terminate the server.
                Dim WorkerThread As Thread
                ' Stop the Reaper Timer.
                ReaperTimer.Enabled = False
                ' Reap all the serving threads.
                ' Reaper timer is stopped, so acquiring mutex is unnecessary.
                For Each WorkerThread In ThreadingPool
                    WorkerThread.Join()
                Next
                ThreadingPool.Clear()
            Catch Ex As ObjectDisposedException
                Exit Do
            End Try
        Loop
    End Sub

    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        OpenFileDialog1.ShowDialog()
        TextBox1.Text = OpenFileDialog1.FileName
    End Sub

    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        TextBox1.Enabled = False
        ServerThread = New Thread(AddressOf ServerThreadMonitor)
        AddStringToLog("Server started at time: " & GetInternetTime() & vbCrLf)
        ServerThread.Start()
        Button1.Enabled = False
        Button2.Enabled = False
        Button3.Enabled = True
    End Sub

    Private Sub TextBox1_DragDrop(ByVal sender As Object, ByVal e As System.Windows.Forms.DragEventArgs) Handles TextBox1.DragDrop
        Dim Paths() As String = e.Data.GetData(DataFormats.FileDrop)
        Dim s As String
        TextBox1.Text = ""
        ' Choose CSignTool.exe
        For Each s In Paths
            If s.EndsWith("CSignTool.exe") Then
                TextBox1.Text = s
                Exit For
            End If
        Next s
        If TextBox1.Text = "" Then
            MsgBox("Please make sure you dropped the correct CSignTool.exe file!", vbExclamation, "Warning")
            TextBox1.Text = Paths(0)
        End If
    End Sub

    Private Sub TextBox1_DragEnter(ByVal sender As Object, ByVal e As System.Windows.Forms.DragEventArgs) Handles TextBox1.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then e.Effect = DragDropEffects.Copy
    End Sub

    Private Sub Form1_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        If Button3.Enabled Then
            MsgBox("The server is still running!", vbExclamation, "Error")
            e.Cancel = True
        End If
    End Sub

    Private Sub Button3_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button3.Click
        Button3.Enabled = False
        ' Stop the server thread monitor and wait for it to close.
        ServerInstance.Close()
        ServerThread.Join()
        AddStringToLog("Server terminated at time: " & GetInternetTime() & vbCrLf)
        TextBox1.Enabled = True
        Button1.Enabled = True
        Button2.Enabled = True
    End Sub

    Private Sub ToolStripMenuItem1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ToolStripMenuItem1.Click
        SaveFileDialog1.ShowDialog()
        Dim fs As Stream = SaveFileDialog1.OpenFile()
        Dim LogBuff() As Byte = Encoding.ASCII.GetBytes(TextBox2.Text)
        fs.Write(LogBuff, 0, LogBuff.Length)
        fs.Close()
    End Sub
End Class
