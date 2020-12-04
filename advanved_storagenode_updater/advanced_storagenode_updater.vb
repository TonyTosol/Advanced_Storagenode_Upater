Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Runtime.InteropServices
Imports System.ServiceProcess
Imports Microsoft.Win32
Imports Newtonsoft.Json.Linq

Public Class advanced_storagenode_updater
    Public Enum ServiceState
        SERVICE_STOPPED = &H1
        SERVICE_START_PENDING = &H2
        SERVICE_STOP_PENDING = &H3
        SERVICE_RUNNING = &H4
        SERVICE_CONTINUE_PENDING = &H5
        SERVICE_PAUSE_PENDING = &H6
        SERVICE_PAUSED = &H7
    End Enum

    <StructLayout(LayoutKind.Sequential)>
    Public Structure ServiceStatus
        Public dwServiceType As Integer
        Public dwCurrentState As ServiceState
        Public dwControlsAccepted As Integer
        Public dwWin32ExitCode As Integer
        Public dwServiceSpecificExitCode As Integer
        Public dwCheckPoint As Integer
        Public dwWaitHint As Integer
    End Structure

    Private NodeData As NodeStruct
    Private data As JObject


    Private random As New Random()

    Protected Overrides Sub OnStart(ByVal args() As String)
        Dim newupdate As New Threading.Thread(AddressOf Update)
        newupdate.IsBackground = True
        newupdate.Start()
        ' Add code here to start your service. This method should set things
        ' in motion so your service can do its work.
    End Sub

    Protected Overrides Sub OnStop()

        ' Add code here to perform any tear-down necessary to stop your service.
    End Sub
    Private Sub StartTimer()
        Dim timer As Double = 60000 * 60 * random.Next(12, 72)
        Update()
        Log("Next update after: " & timer / (60000 * 60) & " h")
        Threading.Thread.Sleep(timer)
        Dim newtimer As New Threading.Thread(AddressOf StartTimer)
        newtimer.IsBackground = True
        newtimer.Start()

    End Sub
    Private Sub Update()

        SearchService()
        data = CheckNewVersion()
        CompareVersions()
        CompareUpdateFile()
        updateNodes()
        Dim newtimer As New Threading.Thread(AddressOf StartTimer)
        newtimer.IsBackground = True
        newtimer.Start()
    End Sub
    Private Sub SearchService()
        Try
            NodeData = New NodeStruct
            Dim services As ServiceController() = ServiceController.GetServices()

            For Each s As ServiceController In ServiceController.GetServices()
                Dim path = GetImagePath(s.ServiceName)

                If path.Contains("storagenode.exe") And Not path.Contains("storagenode-updater.exe") Then
                    Dim Spath = path.Split(Chr(34))





                    Dim newnode As New NodeProp With {.UpdateNeeded = False,
                                                           .Path = Spath(1),
                                                           .ServiceName = s.ServiceName
                                                            }
                    NodeData.Nodes.AddItemToArray(newnode)

                End If
            Next
        Catch ex As Exception
            Log("Search Service Error: " & ex.Message)
        End Try

    End Sub

    Private Function GetImagePath(ServiceName As String) As String
        Dim registryPath As String = "SYSTEM\CurrentControlSet\Services\" & ServiceName
        Dim keyHKLM As RegistryKey = Registry.LocalMachine
        Dim key As RegistryKey

        If Environment.MachineName <> "" Then
            key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, Environment.MachineName).OpenSubKey(registryPath)
        Else
            key = keyHKLM.OpenSubKey(registryPath)
        End If

        Dim value As String = key.GetValue("ImagePath").ToString()
        key.Close()
        key.Dispose()
        Return value
    End Function

    Public Function GetVersion(ByVal fileName As String) As String
        Dim info As System.Diagnostics.FileVersionInfo
        If File.Exists(fileName) Then
            info = System.Diagnostics.FileVersionInfo.GetVersionInfo(fileName)
            Return info.ProductMajorPart & "." & info.ProductMinorPart & "." & info.ProductBuildPart
        Else
            Return ""
        End If
    End Function
    Private Function CheckNewVersion() As JObject
        Dim data As New JObject
        Try


            Dim nodeversionTmp As HttpWebResponse = Nothing

            Dim reader As StreamReader
            Dim request As HttpWebRequest = DirectCast(WebRequest.Create("https://version.storj.io/"), HttpWebRequest)

            nodeversionTmp = DirectCast(request.GetResponse(), HttpWebResponse)
            reader = New StreamReader(nodeversionTmp.GetResponseStream())
            Dim rawresp As String
            rawresp = reader.ReadToEnd()
            data = JObject.Parse(rawresp)("processes")("storagenode")("suggested")
            Dim newversion = (data)("version").ToString
            Log("Last storagenode version is: " & newversion)
            Return data
        Catch ex As Exception
            Log("check new version Error: " & ex.Message)
            Return data
        End Try
    End Function

    Private Sub updateNodes()
        Try
            If NodeData IsNot Nothing Then
                If NodeData.Nodes IsNot Nothing Then
                    For Each node As NodeProp In NodeData.Nodes
                        If node.UpdateNeeded Then

                            If File.Exists(node.Path) Then
                                Dim sc As ServiceController = New ServiceController(node.ServiceName)
                                sc.Stop()
                                Log("Stoping: " & node.ServiceName)
                                Threading.Thread.Sleep(5000)
                                Log("Updating: " & node.ServiceName)

                                My.Computer.FileSystem.CopyFile(My.Application.Info.DirectoryPath & "\storagenode.exe", node.Path, True)
                                Threading.Thread.Sleep(5000)
                                Log("Starting: " & node.ServiceName)
                                sc.Start()
                                Log("Update complete: " & node.ServiceName)
                            Else
                                Log("Cant find storagenode.exe on path " & node.Path)


                            End If
                        Else
                            Log(node.ServiceName & " is up to date")
                        End If
                    Next
                End If
            End If

        Catch ex As Exception


            Log("Update Nodes Error: " & ex.Message)
        End Try
    End Sub
    Private Sub CompareVersions()
        Try
            Dim newversion = (data)("version").ToString
            If NodeData IsNot Nothing Then
                If NodeData.Nodes IsNot Nothing Then
                    For Each node As NodeProp In NodeData.Nodes
                        Dim nodeversion As String = GetVersion(node.Path)
                        Dim nodeversion2 As String = GetVersion(node.Path)
                        If String.Compare(nodeversion, nodeversion2) = 0 Then
                            If String.Compare(newversion, nodeversion) = 0 Then
                            Else
                                node.UpdateNeeded = True
                                Log("Discovered old verion, need update: " & node.ServiceName)
                            End If
                        End If
                    Next
                End If
            End If
        Catch ex As Exception
            Log("compare versions Error: " & ex.Message)
        End Try
    End Sub

    Private Sub CompareUpdateFile()
        Try
            Dim path As String = My.Application.Info.DirectoryPath & "\storagenode.exe"
            Dim newversion As String = (data)("version").ToString
            If File.Exists(path) Then
                Dim ver As String = GetVersion(path)
                If String.Compare(newversion, ver) = 0 Then
                Else
                    File.Delete(path)
                    DownloadNewFile()
                End If
            Else
                DownloadNewFile()
            End If
        Catch ex As Exception
            Log("Get update file Error: " & ex.Message)
        End Try
    End Sub

    Private Sub DownloadNewFile()
        Dim downloadlinc As String = (data)("url").ToString
        downloadlinc = downloadlinc.Replace("{os}", "windows")
        downloadlinc = downloadlinc.Replace("{arch}", "amd64")
        Dim saveAs As String = My.Application.Info.DirectoryPath & "\storagenode.exe.zip"
        Dim theResponse As HttpWebResponse
        Dim theRequest As HttpWebRequest
        Try 'Checks if the file exist
            theRequest = WebRequest.Create(downloadlinc) 'fileUrl is your zip url
            theResponse = theRequest.GetResponse
        Catch ex As Exception
            'could not be found on the server (network delay maybe)
            Exit Sub 'Exit sub or function, because if not found can't be downloaded
        End Try
        Dim length As Long = theResponse.ContentLength
        Dim writeStream As New IO.FileStream(saveAs, IO.FileMode.Create)
        Dim nRead As Integer
        Do
            Dim readBytes(4095) As Byte
            Dim bytesread As Integer = theResponse.GetResponseStream.Read(readBytes, 0, 4096)
            nRead += bytesread
            If bytesread = 0 Then Exit Do
            writeStream.Write(readBytes, 0, bytesread)
        Loop
        theResponse.GetResponseStream.Close()
        writeStream.Close()
        Log("Downloaded new file, extracting")
        'File downloaded 100%
        If File.Exists(saveAs) Then
            extractFile(saveAs, My.Application.Info.DirectoryPath)
            Log("Extracting complete.")
        End If
    End Sub

    Private Sub extractFile(zipPath As String, ExtractPath As String)
        ZipFile.ExtractToDirectory(zipPath, ExtractPath)
    End Sub

    Private Sub Log(log As String)

        Dim strFile As String = My.Application.Info.DirectoryPath & "\advanced-storagenode-updater.log"

        ''Dim length As Integer = log.Length
        Dim bw As BinaryWriter
        Try
            bw = New BinaryWriter(New FileStream(strFile, FileMode.Append))
            Dim datetime As DateTime = DateTime.Now
            bw.Write(datetime.ToString & "  ")

            bw.Write(log & vbCrLf)
            bw.Close()
        Catch e As IOException


        End Try
    End Sub

End Class
