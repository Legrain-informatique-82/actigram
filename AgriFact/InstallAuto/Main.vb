Module Main
    Public Sub Main()
        Try
            Dim doInstall As Boolean
            Dim force As Boolean = False
            Dim args() As String = Environment.GetCommandLineArgs
            Dim par As ParametresInstallation = ParametresInstallation.ParametresDefaut
            If args.Length > 1 Then
                For Each arg As String In args
                    Dim param As CommandParam = CommandParam.Parse(arg)
                    Select Case param.Name
                        Case "-parm"
                            par = ParametresInstallation.LoadXml(param.Value)

                        Case "-help", "-h"
                            MsgBox(Usage)
                            Exit Sub

                        Case "-auto"
                            doInstall = True

                        Case "-force"
                            force = True
                    End Select
                Next
            End If
            If doInstall And par IsNot Nothing Then
                Try
                    Dim t As New TachesInstallation
                    t.parametres = par
                    t.InstallAuto(force)
                Catch ex As Exception
                    MessageBox.Show(ex.Message)
                End Try
                Application.Exit()
            Else
                Application.EnableVisualStyles()
                Dim fr As New FrTachesInstallation(par)
                fr.Show()
                Application.Run(fr)
            End If
        Catch ex As Exception
            MsgBox(ex.Message)

            Throw ex
        End Try
    End Sub

    Private Function Usage() As String
        Return "Parametres : " & vbCrLf & _
                vbTab & "-help, -h : Instructions" & vbCrLf & _
                vbTab & "-parm={chemin fichier} : Fichier XML de configuration � utiliser" & vbCrLf & _
                vbTab & "-auto : Installation automatique" & vbCrLf & _
                vbTab & "-force : Forcer la r�installation"
    End Function

    Private Class CommandParam
        Public Name As String
        Public Value As String
        Public Sub New()
        End Sub
        Public Sub New(ByVal arg As String)
            Dim c As CommandParam = Parse(arg)
            Me.Name = c.Name
            Me.Value = c.Value
        End Sub
        Public Shared Function Parse(ByVal arg As String) As CommandParam
            Dim c As New CommandParam
            If arg.IndexOf("=") >= 0 Then
                c.Name = arg.Substring(0, arg.IndexOf("=")).ToLower
                c.Value = arg.Substring(arg.IndexOf("=") + 1).Replace(Chr(34), "")
            Else
                c.Name = arg.ToLower
                c.Value = ""
            End If
            Return c
        End Function
    End Class
End Module
