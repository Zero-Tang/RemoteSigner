Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Public Class Form1
    Private Sub SendSignRequest()
        ' Read the file.
        Dim fs As New FileStream(TextBox1.Text, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        If fs.Length > &H7FFFFFFF Then
            MsgBox("The file is too big!", vbExclamation, "Error")
            Exit Sub
        End If
        Dim FileBuff() As Byte
        ReDim FileBuff(fs.Length - 1)
        fs.Read(FileBuff, 0, fs.Length)
        ' Initialize the socket and connect to server.
        Dim ClientInstance As New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        ClientInstance.Connect(TextBox2.Text, CInt(TextBox3.Text))
        ' Construct request header.
        Dim RequestHeader(19) As Byte
        Array.Copy(Encoding.ASCII.GetBytes("Sign Request #ZT"), 0, RequestHeader, 0, 16)
        Array.Copy(BitConverter.GetBytes(CInt(fs.Length)), 0, RequestHeader, 16, 4)
        ' Send the request header.
        Dim SendCounter As Integer = 0
        Do While SendCounter < 20
            SendCounter += ClientInstance.Send(RequestHeader, SendCounter, 20 - SendCounter, SocketFlags.None)
        Loop
        ' Send the rule name.
        Dim RuleNameBuffer() As Byte = Encoding.ASCII.GetBytes(TextBox4.Text)
        ReDim Preserve RuleNameBuffer(255)
        RuleNameBuffer(255) = CByte(Encoding.ASCII.GetByteCount(TextBox4.Text))
        SendCounter = 0
        Do While SendCounter < 256
            SendCounter += ClientInstance.Send(RuleNameBuffer, SendCounter, 256 - SendCounter, SocketFlags.None)
        Loop
        ' Send the file content.
        SendCounter = 0
        Do While SendCounter < fs.Length
            SendCounter += ClientInstance.Send(FileBuff, SendCounter, fs.Length - SendCounter, SocketFlags.None)
        Loop
        ' Server is now signing stuff. Leave the driver as backup.
        fs.Close()
        ' Receive the reply header.
        Dim ReplyHeader(19) As Byte
        Dim RecvCounter As Integer = 0
        Do While RecvCounter < 20
            RecvCounter = ClientInstance.Receive(ReplyHeader, RecvCounter, 20 - RecvCounter, SocketFlags.None)
        Loop
        ' Check the signature
        Dim Signature As String = Encoding.ASCII.GetString(ReplyHeader, 0, 16)
        If Signature = "Sign Reply #ZT-S" Then
            Dim FileSize As Integer = BitConverter.ToInt32(ReplyHeader, 16)
            ReDim FileBuff(FileSize - 1)
            fs = New FileStream(TextBox1.Text & ".signed.sys", FileMode.Create, FileAccess.ReadWrite, FileShare.None)
            RecvCounter = 0
            Do While RecvCounter < FileSize
                RecvCounter += ClientInstance.Receive(FileBuff, RecvCounter, FileSize - RecvCounter, SocketFlags.None)
            Loop
            fs.Write(FileBuff, 0, FileSize)
            fs.Close()
        Else
            MsgBox("Server replied with error signature: " & Signature, vbExclamation, "Error")
        End If
        ClientInstance.Close()
    End Sub

    Private Sub TextBox1_DragDrop(ByVal sender As Object, ByVal e As System.Windows.Forms.DragEventArgs) Handles TextBox1.DragDrop
        Dim Paths() As String = e.Data.GetData(DataFormats.FileDrop)
        Dim s As String
        TextBox1.Text = ""
        ' Choose file with driver name extension.
        For Each s In Paths
            If s.EndsWith(".sys") Then
                TextBox1.Text = s
                Exit For
            End If
        Next s
        If TextBox1.Text = "" Then
            MsgBox("Please make sure you dropped a driver file!", vbExclamation, "Warning")
            TextBox1.Text = Paths(0)
        End If
    End Sub

    Private Sub TextBox1_DragEnter(ByVal sender As Object, ByVal e As System.Windows.Forms.DragEventArgs) Handles TextBox1.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then e.Effect = DragDropEffects.Copy
    End Sub

    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        OpenFileDialog1.ShowDialog()
    End Sub

    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        Button1.Enabled = False
        Button2.Enabled = False
        Call SendSignRequest()
        Button2.Enabled = True
        Button1.Enabled = True
    End Sub
End Class
