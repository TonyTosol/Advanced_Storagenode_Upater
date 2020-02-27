Imports System.ComponentModel
Imports System.Configuration.Install
Imports System.ServiceProcess

Public Class ProjectInstaller

    Public Sub New()
        MyBase.New()

        'This call is required by the Component Designer.
        InitializeComponent()

        'Add initialization code after the call to InitializeComponent

    End Sub

    Private Sub ServiceInstaller1_AfterInstall(sender As Object, e As InstallEventArgs) Handles ServiceInstaller1.AfterInstall
        Dim sc As New ServiceController()
        sc.ServiceName = ServiceInstaller1.ServiceName

        If sc.Status = ServiceControllerStatus.Stopped Then
            Try
                ' Start the service, and wait until its status is "Running".
                sc.Start()
                sc.WaitForStatus(ServiceControllerStatus.Running)

                ' TODO: log status of service here: sc.Status
            Catch ex As Exception
                ' TODO: log an error here: "Could not start service: ex.Message"
                Throw
            End Try
        End If
    End Sub
End Class
