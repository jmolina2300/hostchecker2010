Imports System.IO
Imports System.Net.Sockets
Imports System.Net.NetworkInformation
Imports System.Net


Public Class frmMain
    Private Const WINDOW_TITLE As String = "Host Checker"
    Private Const SAVE_FILE_NAME As String = "myhosts.txt"
    Private Const SAVE_DIRECTORY As String = ".\"
    Private Const SAVE_FILE_PATH As String = SAVE_DIRECTORY & SAVE_FILE_NAME

    Private Const COLUMN_DESCRIPTION As Integer = 1
    Private Const COLUMN_ADDRESS As Integer = 2
    Private Const COLUMN_SERVER As Integer = 3
    Private Const STATUS As Integer = 0

    Private Const DELIMETER As String = vbTab
    Private COLOR_UP As Long = RGB(100, 255, 100)
    Private COLOR_DOWN As Long = RGB(255, 100, 100)

    Private numHosts As Integer = 0
    Private hasChanges As Boolean = False

    Private myThreadPool As Generic.List(Of System.Threading.Thread)




    Private Sub frmMain_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        Me.Text = WINDOW_TITLE
        Me.myThreadPool = New Generic.List(Of System.Threading.Thread)
    End Sub


    Private Sub stripTabCharacter(ByRef str As String)
        str = str.Replace(vbTab, " ")
    End Sub

    Private Function tryHttp()
        Return False
    End Function

    Private Function tryHttpSecure()
        Return False
    End Function

    ' Get the server type by sending a HEAD request
    Private Function getServerType(ByVal serverAddress As String)
        Dim newServerAddress = serverAddress
        If Not newServerAddress.Contains("http://") Then
            newServerAddress = "http://" & serverAddress
        End If
        Dim request As WebRequest = WebRequest.Create(newServerAddress)
        request.Credentials = CredentialCache.DefaultCredentials
        request.Method = "GET"
        request.Timeout = 3000

        Dim response As HttpWebResponse
        Try
            response = CType(request.GetResponse(), HttpWebResponse)
            If response.StatusCode = HttpStatusCode.OK Then
                Dim serverType As String = response.Server.ToString()
                Return serverType
            Else
                Return "Unknown"
            End If
        Catch ex As Exception
            Return "Unknown"
        End Try

    End Function

    Private Function isConnected(ByRef address As String)
        Dim tcpClient As TcpClient
        Try
            tcpClient = New TcpClient(address, 80)
            tcpClient.SendTimeout = 100
            tcpClient.ReceiveTimeout = 100
            If tcpClient.Connected Then
                tcpClient.Close()
                Return True
            End If
        Catch ex As SocketException
            Console.WriteLine("SocketException: {0}", address)
        End Try


        ' Try ping
        Dim pingSuccess As Boolean = False
        Try
            Dim myPing As New Ping
            pingSuccess = myPing.Send(address, 500).Status = IPStatus.Success
        Catch ex As PingException
            pingSuccess = False
        End Try

        Return pingSuccess
    End Function


    ' Test connection of of a single host
    Private Sub testConnection(ByVal rowIndex As Integer)

        Dim currentAddress As String = dgvHosts.Rows(rowIndex).Cells(COLUMN_ADDRESS).Value.ToString()
        Dim success As Boolean = isConnected(currentAddress)

        If success = True Then
            dgvHosts.Rows(rowIndex).Cells(0).Value = "UP"
            dgvHosts.Rows(rowIndex).Cells(0).Style.BackColor = Color.LightGreen
            ' Try to get the server type
            Dim serverType As String = getServerType(currentAddress)
            dgvHosts.Rows(rowIndex).Cells(COLUMN_SERVER).Value = serverType
        Else
            dgvHosts.Rows(rowIndex).Cells(0).Value = "DOWN"
            dgvHosts.Rows(rowIndex).Cells(0).Style.BackColor = Color.LightPink
        End If

    End Sub
    ' Test Connections of all hosts
    Private Sub testAllHostConnections()
        numHosts = dgvHosts.RowCount
        For row As Integer = 0 To dgvHosts.Rows.Count - 1
            Dim currentAddress As String = dgvHosts.Rows(row).Cells(COLUMN_ADDRESS).Value.ToString()

            ' Test Connection of current address
            Dim myThread As New Threading.Thread(AddressOf testConnection)
            myThread.Start(row)
            myThreadPool.Add(myThread)

            bgwTestConnection.ReportProgress(row)

        Next
    End Sub

    Private Sub pushChange()
        Me.Text = WINDOW_TITLE & " (*)"
        hasChanges = True
    End Sub

    Private Sub resetChanges()
        Me.Text = WINDOW_TITLE
        hasChanges = False
    End Sub

    Private Function unsavedChanges() As Boolean
        Return hasChanges
    End Function




    Private Function saveFileExists() As Boolean
        Dim saveFileName As String = SAVE_FILE_NAME
        Dim saveDirectory As String = SAVE_DIRECTORY
        Dim Filename As String = System.IO.Path.GetFileName(saveFileName)
        Dim SavePath As String = System.IO.Path.Combine(saveDirectory, Filename)

        Return System.IO.File.Exists(SavePath)
    End Function

    ' Save the hosts to a file
    Private Sub saveHosts()

        If saveFileExists() Then
            Dim result As Integer = MessageBox.Show("Saved data exists. Overwrite?", "File Exists",
                     MessageBoxButtons.YesNo)
            If result = DialogResult.No Then
                Return
            End If
        End If

        Dim writer As New IO.StreamWriter(SAVE_FILE_PATH)
        For row As Integer = 0 To dgvHosts.Rows.Count - 1
            For col As Integer = 1 To dgvHosts.Columns.Count - 1
                If col = dgvHosts.Columns.Count Then
                    writer.Write(dgvHosts.Rows(row).Cells(col).Value)
                Else
                    writer.Write(dgvHosts.Rows(row).Cells(col).Value & DELIMETER)
                End If

            Next
            writer.WriteLine()
        Next
        writer.Close()

    End Sub

    ' Import the hosts from a file
    Private Sub importHosts()
        Using reader As New IO.StreamReader(SAVE_FILE_PATH)
            Dim line As String
            ' Clear all the data first
            dgvHosts.Rows.Clear()

            ' Read in all the data
            Do While reader.Peek <> -1
                line = reader.ReadLine()
                Dim parts As String() = line.Split(DELIMETER)
                Dim hostDescription As String = parts(0)    ' Split line at delimiter
                Dim hostAddress As String = parts(1)
                dgvHosts.Rows.Add(" ", hostDescription, hostAddress)
            Loop
            reader.Close()
        End Using
    End Sub

    Private Sub btnSave_Click(sender As System.Object, e As System.EventArgs) Handles btnSave.Click
        If dgvHosts.Rows.Count > 0 Then
            saveHosts()
            resetChanges()
        End If
    End Sub

    Private Sub btnImport_Click(sender As System.Object, e As System.EventArgs) Handles btnImport.Click
        If unsavedChanges() Then
            Dim result As Integer = MessageBox.Show("You have unsaved changes. Continue?", "Unsaved Changes",
                     MessageBoxButtons.YesNo)
            If result = DialogResult.No Then
                Return
            End If
        End If
        importHosts()
        resetChanges()
    End Sub

    Private Sub btnTest_Click(sender As System.Object, e As System.EventArgs) Handles btnTest.Click
        For row As Integer = 0 To dgvHosts.Rows.Count - 1
            dgvHosts.Rows(row).Cells(STATUS).Style.BackColor = Color.White
            dgvHosts.Rows(row).Cells(COLUMN_SERVER).Value = Nothing

        Next

        btnTest.Enabled = False
        Cursor.Current = Cursors.WaitCursor
        bgwTestConnection.RunWorkerAsync()
    End Sub

    Private Sub bgwTestConnection_DoWork(sender As System.Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgwTestConnection.DoWork
        testAllHostConnections()
    End Sub

    Private Sub bgwTestConnection_RunWorkerCompleted(sender As System.Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwTestConnection.RunWorkerCompleted
        btnTest.Enabled = True
        Cursor.Current = Cursors.Default
    End Sub
    ' Add host from Dialog Box
    Private Sub addHost(address As String, description As String)
        Dim emptyAddress As Boolean = (address.Length = 0)
        Dim emptyDescription As Boolean = (description.Length = 0)
        If emptyAddress Then
            Exit Sub
        End If
        Dim actualDescription As String
        If Not emptyDescription Then
            actualDescription = description
        Else
            actualDescription = "-"
        End If
        dgvHosts.Rows.Add(" ", actualDescription, address)
        pushChange()
    End Sub
    Private Sub btnAdd_Click(sender As System.Object, e As System.EventArgs) Handles btnAdd.Click
        Dim dlg As dlgAddHost = New dlgAddHost()
        dlg.ShowDialog()
        If dlg.DialogResult = Windows.Forms.DialogResult.OK Then
            addHost(dlg.address, dlg.description)
        End If

    End Sub

    Private Sub bgwTestConnection_ProgressChanged(sender As System.Object, e As System.ComponentModel.ProgressChangedEventArgs) Handles bgwTestConnection.ProgressChanged
        Console.WriteLine(e.ProgressPercentage)
    End Sub

    Private Sub btnRemove_Click(sender As System.Object, e As System.EventArgs) Handles btnRemove.Click
        ' Exit if the dataGridView is empty
        If dgvHosts.RowCount = 0 Then
            Exit Sub
        End If
        ' Exit if nothing is selected
        If dgvHosts.SelectedRows.Count = 0 Then
            Exit Sub
        End If

        Dim i As Integer = dgvHosts.SelectedRows(0).Index
        dgvHosts.Rows.RemoveAt(i)
        pushChange()

    End Sub

    Private Sub btnAbort_Click(sender As System.Object, e As System.EventArgs) Handles btnAbort.Click
        For Each thread In myThreadPool
            thread.Abort()
        Next
        myThreadPool.Clear()
    End Sub


End Class
