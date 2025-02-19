Imports ICSharpCode.SharpZipLib.Zip

Public Class GestionSauvegarde

#Region "Utils de remont�e de progression"
    Public Event ReportsProgress(ByVal sender As Object, ByVal e As ProgressEventArgs)

    Public Class ProgressEventArgs
        Private _status As String
        Public Property Status() As String
            Get
                Return _status
            End Get
            Set(ByVal Value As String)
                _status = Value
            End Set
        End Property

        Private _progress As Integer
        Public Property Progress() As Integer
            Get
                Return _progress
            End Get
            Set(ByVal Value As Integer)
                _progress = Value
            End Set
        End Property

        Public Sub New()

        End Sub

        Public Sub New(ByVal progress As Integer)
            Me.New()
            Me.Progress = progress
        End Sub

        Public Sub New(ByVal progress As Integer, ByVal status As String)
            Me.New()
            Me.Progress = progress
            Me.Status = status
        End Sub

    End Class

    Private Sub ReportProgress(ByVal progress As Integer, ByVal status As String)
        RaiseEvent ReportsProgress(Me, New ProgressEventArgs(progress, status))
        Application.DoEvents()
    End Sub
#End Region

#Region "M�thodes diverses"
    'A mettre dans l'archive : 
    ' - Donn�es de la base (Backup de la base) seulement si on est sur le serveur de donn�es
    ' - Fichier parametres
    ' - Repertoire "Etats"
    ' - Fichier Manifest de la sauvegarde
    Public Sub Sauvegarder(ByVal fichierSauvegarde As String)

        ReportProgress(0, "Initialisation")

        'Cr�er un r�pertoire temporaire
        Dim tempDir As String = String.Format("{0}AgrifactSave{1:yyMMddHHmmss}", IO.Path.GetTempPath, Now)
        IO.Directory.CreateDirectory(tempDir)

        Try

            Dim m As New ManifestSauvegarde

            'Enregistrer la base de donn�es
            ReportProgress(10, "Sauvegarde des donn�es")
            BackupDatabase(DefinitionDonnees.GetstrConnexion("sa", ""), tempDir & "\Donnees.bk")
            m.Items.Add(New DatabaseBackup("Base de donn�es", "Donnees.bk", ""))

            'Copier le fichier "Parametres"
            ReportProgress(40, "Sauvegarde des param�tres")
            Dim nonFichierParam As String = IO.Path.GetFileName(ParametresApplication.CheminFichierParam)
            IO.File.Copy(ParametresApplication.CheminFichierParam, IO.Path.Combine(tempDir, nonFichierParam))
            m.Items.Add(New Fichier("Param�trage", nonFichierParam, True))

            'Copier le repertoire Etats
            ReportProgress(50, "Sauvegarde des �tats")
            CopyDir(Application.StartupPath & "\Etats", tempDir)
            m.Items.Add(New Dossier("Impressions et rapports", "Etats"))

            If IO.Directory.Exists(Application.StartupPath & "\EtatsTiers") Then
                CopyDir(Application.StartupPath & "\EtatsTiers", tempDir)
                m.Items.Add(New Dossier("Impressions et rapports sp�cifiques", "EtatsTiers"))
            End If

            'Ajouter les r�pertoires RptSats
            If IO.Directory.Exists(Application.StartupPath & "\RptStats") Then
                CopyDir(Application.StartupPath & "\RptStats", tempDir)
                m.Items.Add(New Dossier("Impressions et rapports persos", "RptStats"))
            End If

            If IO.Directory.Exists(Application.StartupPath & "\RptStatsClients") Then
                CopyDir(Application.StartupPath & "\RptStatsClients", tempDir)
                m.Items.Add(New Dossier("Impressions et rapports persos clients", "RptStatsClients"))
            End If

            If IO.Directory.Exists(Application.StartupPath & "\RptStatsProduits") Then
                CopyDir(Application.StartupPath & "\RptStatsProduits", tempDir)
                m.Items.Add(New Dossier("Impressions et rapports persos produits", "RptStatsProduits"))
            End If

            'Ajouter le fichier Manifest
            m.Enregistrer(tempDir & "\MANIFEST.XML")

            'Zipper le r�sultat dans le nom de fichier pass� en param�tre
            ReportProgress(75, "Compression des donn�es")
            Dim fz As New FastZip
            fz.CreateZip(fichierSauvegarde, tempDir, True, "", "")

            'Supprimer le r�pertoire temporaire
            ReportProgress(100, "Finalisation")
        Catch ex As Exception
            Throw ex
        Finally
            Try
                IO.Directory.Delete(tempDir, True)
            Catch
            End Try
        End Try
    End Sub

    Public Sub Restaurer(ByVal fichierSauvegarde As String, ByVal repDest As String)

        'Cr�er un r�pertoire temporaire
        Dim tempDir As String = String.Format("{0}AgrifactRestore{1:yyMMddHHmmss}", IO.Path.GetTempPath, Now)
        IO.Directory.CreateDirectory(tempDir)

        Try
            Dim fz As New FastZip
            fz.ExtractZip(fichierSauvegarde, tempDir, "")

            Dim m As ManifestSauvegarde = LireEtVerifierManifest(tempDir)

            'Faire choisir les items � restaurer
            Dim FrChoix As New FrChoixRestauration(m)
            If FrChoix.ShowDialog() = DialogResult.Cancel Then
                Throw New Exception("Restauration annul�e")
            Else
                m = FrChoix.Selection
            End If

            ReportProgress(10, "V�rification de la sauvegarde")
            If Not IO.Directory.Exists(repDest) Then IO.Directory.CreateDirectory(repDest)

            Dim i As Integer = 0
            For Each item As ItemBackup In m.Items
                i += 1
                ReportProgress((i * 80 \ m.Items.Count) + 10, "Restauration en cours : " & item.description)
                If TypeOf item Is DatabaseBackup Then
                    'Restauration d'une base de donn�es
                    Try
                        Dim db As DatabaseBackup = CType(item, DatabaseBackup)
                        RestoreDatabase(DefinitionDonnees.GetstrConnexion("sa", ""), tempDir & "\" & db.fichierBackup, db.nomBase)
                    Catch ex As Exception
                        MsgBox(ex.Message, MsgBoxStyle.Exclamation, "Avertissement")
                    End Try
                ElseIf TypeOf item Is Dossier Then
                    'Restauration d'un dossier
                    Dim d As Dossier = CType(item, Dossier)
                    CopyDir(tempDir & "\" & d.nomDossier, repDest)
                ElseIf TypeOf item Is Fichier Then
                    'Restauration d'un fichier
                    Dim f As Fichier = CType(item, Fichier)
                    Dim fichierOri As String = repDest & "\" & f.nomFichier
                    If IO.File.Exists(fichierOri) And f.sauvegarderFichierAvantRemplacement Then
                        'Eventuellement sauvegarde du fichier existant
                        IO.File.Copy(fichierOri, fichierOri & ".bak", True)
                    End If
                    IO.File.Copy(tempDir & "\" & f.nomFichier, fichierOri, True)
                Else
                    'Type de donn�es � r�cup�rer inconnu
                End If
            Next

        Catch ex As ZipException
            Throw New Exception("Fichier sauvegarde invalide." & vbCrLf & "Restauration impossible.")
        Catch ex As Exception
            Throw ex
        Finally
            ReportProgress(100, "Finalisation")
            IO.Directory.Delete(tempDir, True)
        End Try
    End Sub

    Private Function LireEtVerifierManifest(ByVal repDonnees As String) As ManifestSauvegarde
        Dim m As ManifestSauvegarde

        Try
            'Charger le MANIFEST
            m = ManifestSauvegarde.Charger(repDonnees & "\MANIFEST.XML")
        Catch ex As Exception
            'Plante si le fichier est absent ou endommag�
            If MsgBox("La sauvegarde semble endommag�e." & vbCrLf & _
                    "La restauration risque de rencontrer des probl�mes." & vbCrLf & _
                    "Souhaitez-vous continuer ?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                'Cr�er un MANIFEST standard pour tenter la restauration
                m = New ManifestSauvegarde
                m.Items.Add(New DatabaseBackup("Base de donn�es", "Donnees.bk", ""))
                m.Items.Add(New Fichier("Param�trage", "Parametres.xml", True))
                m.Items.Add(New Dossier("Impressions et rapports", "Etats"))
                m.Items.Add(New Dossier("Impressions et rapports sp�cifiques", "EtatsTiers"))
                m.Items.Add(New Dossier("Impressions et rapports persos", "RptStats"))
                m.Items.Add(New Dossier("Impressions et rapports persos clients", "RptStatsClients"))
                m.Items.Add(New Dossier("Impressions et rapports persos produits", "RptStatsProduits"))
            Else
                Throw New Exception("Restauration annul�e")
            End If
        End Try


        'V�rification de la version de sauvegarde
        Select Case m.Version
            Case Is <= ManifestSauvegarde.VERSION_COURANTE
                'Faire ici les traitements qui s'imposent pour la r�trocompatibilit�
                'Restore puis DatabaseUpdate ?
            Case Else 'Cas des versions de sauvegarde non g�r�es
                If MsgBox("Cette sauvegarde a �t� produite par une version plus r�cente de l'application." & vbCrLf & _
                "La restauration risque de rencontrer des probl�mes." & vbCrLf & _
                "Souhaitez-vous continuer ?", MsgBoxStyle.YesNo) = MsgBoxResult.No Then
                    Throw New Exception("Restauration annul�e")
                End If
        End Select

        'Pour chaque Item du MANIFEST, v�rifier que les �l�ments sont pr�sents
        For i As Integer = m.Items.Count - 1 To 0 Step -1
            Dim item As ItemBackup = CType(m.Items(i), ItemBackup)
            Dim verif As Boolean = True
            If TypeOf item Is DatabaseBackup Then
                verif = IO.File.Exists(repDonnees & "\" & CType(item, DatabaseBackup).fichierBackup)
            ElseIf TypeOf item Is Fichier Then
                verif = IO.File.Exists(repDonnees & "\" & CType(item, Fichier).nomFichier)
            ElseIf TypeOf item Is Dossier Then
                verif = IO.Directory.Exists(repDonnees & "\" & CType(item, Dossier).nomDossier)
            Else
                'Type de donn�es � r�cup�rer inconnu
            End If

            'Si absence, demander � l'utilisateur si la restauration doit quand m�me avoir lieu
            If Not verif Then
                If MsgBox("L'�l�ment '" & item.description & "' n'est pas pr�sent dans la sauvegarde." & vbCrLf & _
                    "Cet �l�ment ne pourra pas �tre restaur�." & vbCrLf & _
                    "Souhaitez-vous continuer ?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                    'Supprimer l'item du manifest utilis� pour la restauration
                    m.Items.RemoveAt(i)
                Else
                    Throw New Exception("Restauration annul�e")
                End If
            End If
        Next

        If m.Items.Count = 0 Then
            Throw New Exception("Aucun �l�ment n'est r�cup�rable." & vbCrLf & "Restauration annul�e.")
        End If

        Return m
    End Function
#End Region

#Region "Fonctions SQL Server"
    Private Shared Sub BackupDatabase(ByVal connectionString As String, ByVal filename As String)
        Dim conn As New SqlClient.SqlConnection(connectionString)
        Try
            conn.Open()
            'V�rification qu'on se trouve bien sur le serveur de donn�es
            Dim cmd As New SqlClient.SqlCommand("SELECT SERVERPROPERTY('MachineName')", conn)
            Dim serverName As String = CStr(cmd.ExecuteScalar)
            If String.Compare(serverName, Environment.MachineName, True) <> 0 Then
                Throw New Exception("La sauvegarde des donn�es est impossible depuis un poste distant." & vbCrLf & _
                                    "Pour sauvegarder les donn�es vous devez executer l'application sur le poste h�bergeant la base de donn�es.")
            Else
                cmd.CommandText = String.Format("BACKUP DATABASE {0} TO DISK='{1}' WITH FORMAT,INIT", conn.Database, filename)
                cmd.ExecuteNonQuery()
            End If
        Catch ex As Exception
            Throw ex
        Finally
            If conn.State <> ConnectionState.Closed Then conn.Close()
        End Try
    End Sub

    Private Shared Sub RestoreDatabase(ByVal connectionString As String, ByVal filename As String, Optional ByVal baseARestaurer As String = "")
        Dim conn As New SqlClient.SqlConnection(connectionString)
        Try
            conn.Open()

            'R�cuperer le nom et le serveur de la base Agrifact courante 
            '(pour savoir si c'est une restauration ou un transfert de donn�es)
            Dim curBaseName As String = CStr(ParametresApplication.ValeurParametre("BASESQL", ""))
            Dim curServerName As String = CStr(ParametresApplication.ValeurParametre("ServeurSQL", ""))

            'V�rification qu'on se trouve bien sur le serveur de donn�es
            Dim cmd As New SqlClient.SqlCommand("SELECT SERVERPROPERTY('MachineName')", conn)
            Dim serverName As String = CStr(cmd.ExecuteScalar)
            If String.Compare(serverName, Environment.MachineName, True) <> 0 Then
                Throw New Exception("L'import des donn�es est impossible depuis un poste distant." & vbCrLf & _
                                    "Pour importer les donn�es vous devez executer l'application sur le poste h�bergeant la base de donn�es.")
            End If

            'Se placer sur la base MASTER
            cmd.CommandText = "USE MASTER"
            cmd.ExecuteNonQuery()

            'R�cup�rer le nom et le serveur de la base de donn�es dans le fichier de sauvegarde
            cmd.CommandText = String.Format("RESTORE HEADERONLY FROM DISK='{0}' WITH FILE=1", filename)
            Dim srcBaseName As String
            Dim srcServerName As String
            Dim dr As SqlClient.SqlDataReader = cmd.ExecuteReader
            If dr.Read Then
                srcBaseName = CStr(dr("DatabaseName"))
                srcServerName = CStr(dr("ServerName"))
            End If
            dr.Close()

            Dim sqlRestore As String
            If (baseARestaurer = "" Or baseARestaurer = srcBaseName) And srcBaseName = curBaseName And srcServerName = curServerName Then
                'On restaure la base telle qu'elle a �t� sauvegard�e (la sauvegarde a bien �t� faite sur le poste en cours, pas de gestion de fichier � faire)
                baseARestaurer = srcBaseName
                sqlRestore = String.Format("RESTORE DATABASE {0} FROM DISK='{1}' WITH REPLACE", baseARestaurer, filename)
            Else
                'On restaure la base sauvegard�e dans une nouvelle base ou sur une nouvelle machine


                'Trouver les noms logiques des fichiers de la base
                Dim dataLogicalFile As String, logLogicalFile As String
                cmd.CommandText = String.Format("RESTORE FILELISTONLY FROM DISK='{0}' WITH FILE=1", filename)
                dr = cmd.ExecuteReader
                While dr.Read
                    Select Case CStr(dr("Type"))
                        Case "D"
                            dataLogicalFile = CStr(dr("LogicalName"))
                        Case "L"
                            logLogicalFile = CStr(dr("LogicalName"))
                    End Select
                End While
                dr.Close()

                If baseARestaurer = "" Or baseARestaurer = curBaseName Then
                    baseARestaurer = curBaseName

                    'Trouver les noms physiques des fichiers actuels
                    Dim dataPhysicalFile As String, logPhysicalFile As String
                    cmd.CommandText = String.Format("SELECT * FROM {0}..sysfiles", baseARestaurer)
                    dr = cmd.ExecuteReader
                    While dr.Read
                        Select Case CInt(dr("groupid"))
                            Case 1
                                dataPhysicalFile = CStr(dr("filename")).Trim
                            Case 0
                                logPhysicalFile = CStr(dr("filename")).Trim
                        End Select
                    End While
                    dr.Close()

                    sqlRestore = String.Format("RESTORE DATABASE {0} FROM DISK='{1}' WITH REPLACE, MOVE '{2}' TO '{4}', MOVE '{3}' TO '{5}'", baseARestaurer, filename, dataLogicalFile, logLogicalFile, dataPhysicalFile, logPhysicalFile)
                Else
                    'C'est une nouvelle base, on pose les fichiers dans un nouvel emplacement
                    Dim PhysicalFile As String = Application.StartupPath & "\Data\" & baseARestaurer
                    sqlRestore = String.Format("RESTORE DATABASE {0} FROM DISK='{1}' WITH REPLACE, MOVE '{2}' TO '{4}.mdf', MOVE '{3}' TO '{4}.ldf'", baseARestaurer, filename, dataLogicalFile, logLogicalFile, PhysicalFile)
                End If
            End If

            'Placer la base en SINGLE USER pour tuer les connexions existantes
            cmd.CommandText = String.Format("ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", baseARestaurer)
            cmd.ExecuteNonQuery()

            'Execution de la commande de restauration
            cmd.CommandText = sqlRestore
            cmd.ExecuteNonQuery()

            cmd.CommandText = String.Format("ALTER DATABASE {0} SET MULTI_USER WITH ROLLBACK IMMEDIATE", baseARestaurer)
            cmd.ExecuteNonQuery()
            conn.Close()

            'Recr�er les utilisateurs pour r�parer les connexions
            RecreerUtilisateurs(connectionString, baseARestaurer)

        Catch ex As Exception
            Throw ex
        Finally
            Try
                If conn.State <> ConnectionState.Closed Then
                    Dim cmd As New SqlClient.SqlCommand(String.Format("ALTER DATABASE {0} SET MULTI_USER WITH ROLLBACK IMMEDIATE", baseARestaurer), conn)
                    cmd.ExecuteNonQuery()
                    conn.Close()
                End If
            Catch
            End Try
        End Try

    End Sub

    Public Shared Sub RecreerUtilisateurs(ByVal connectionString As String, ByVal base As String)
        Dim conn As New SqlClient.SqlConnection(connectionString)
        conn.Open()

        If conn.Database.ToLower <> base.ToLower Then
            Dim cmd As New SqlClient.SqlCommand("USE " & base, conn)
            cmd.ExecuteNonQuery()
        End If

        Dim rapport As New System.Text.StringBuilder
        Dim dt As New DataTable
        Dim dta As New SqlClient.SqlDataAdapter("Select Utilisateur, Password From Utilisateurs", conn)
        dta.Fill(dt)

        For Each rw As DataRow In dt.Rows
            Try
                RecreerUtilisateur(conn, Convert.ToString(rw("Utilisateur")), Convert.ToString(rw("Password")), base)
            Catch ex As Exception
                rapport.AppendFormat("Erreur dans la cr�ation de l'utilisateur {0} : {1}" & vbCrLf, rw("Utilisateur"), ex.Message)
            End Try
        Next
        conn.Close()

        If rapport.Length > 0 Then
            Throw New Exception(rapport.ToString)
        End If

    End Sub

    Private Shared Sub RecreerUtilisateur(ByRef conn As SqlClient.SqlConnection, ByVal Utilisateur As String, ByVal Password As String, ByVal BaseSql As String)
        Dim cmd As New SqlClient.SqlCommand("", conn)
        cmd.CommandText = "select count(*) from master.dbo.syslogins where name='" & Utilisateur & "'"
        If CInt(cmd.ExecuteScalar()) = 0 Then
            'Si le login n'existe pas, le cr�er
            cmd.CommandText = String.Format("Exec sp_addlogin @loginame=N'{0}',@passwd=N'{1}',@defdb={2}", Utilisateur, Password, BaseSql)
            cmd.ExecuteNonQuery()
        Else
            'Sinon MAJ son Password
            cmd.CommandText = String.Format("Exec sp_password NULL, '{1}', '{0}'", Utilisateur, Password)
            cmd.ExecuteNonQuery()
        End If

        'Si l'utilisateur de BDD existe, le supprimer
        Try
            cmd.CommandText = String.Format("Exec sp_dropuser '{0}'", Utilisateur)
            cmd.ExecuteNonQuery()
        Catch ex As SqlClient.SqlException
            If ex.Number <> 15008 Then Throw ex
        End Try

        'Cr�er le user dans la base dans le groupe "Utilisateurs"
        cmd.CommandText = String.Format("Exec sp_adduser '{0}','{0}','{1}'", Utilisateur, "Utilisateurs")
        cmd.ExecuteNonQuery()
    End Sub
#End Region

#Region "M�thodes partag�es"
    Private Shared Sub CopyDir(ByVal dirSrc As String, ByVal dirDest As String, Optional ByVal overwrite As Boolean = True, Optional ByVal recurse As Boolean = True)
        Dim fics() As String = IO.Directory.GetFiles(dirSrc)
        If fics.Length > 0 Then
            Dim dirCopy As String = dirDest & "\" & IO.Path.GetFileName(dirSrc)
            If Not IO.Directory.Exists(dirCopy) Then IO.Directory.CreateDirectory(dirCopy)
            For Each fic As String In fics
                IO.File.Copy(fic, dirCopy & "\" & IO.Path.GetFileName(fic), overwrite)
            Next
        End If
        If recurse Then
            Dim dirs() As String = IO.Directory.GetDirectories(dirSrc)
            If dirs.Length > 0 Then
                Dim dirCopy As String = dirDest & "\" & IO.Path.GetFileName(dirSrc)
                If Not IO.Directory.Exists(dirCopy) Then IO.Directory.CreateDirectory(dirCopy)
                For Each dir As String In dirs
                    CopyDir(dir, dirCopy, overwrite, recurse)
                Next
            End If
        End If
    End Sub
#End Region

#Region "Structure du Manifest de la sauvegarde"
    Public Class ManifestSauvegarde
        Public Const VERSION_COURANTE As String = "1.0"
        <Xml.Serialization.XmlAttribute()> Public Version As String

        <Xml.Serialization.XmlArray(), _
        Xml.Serialization.XmlArrayItem(GetType(DatabaseBackup)), _
        Xml.Serialization.XmlArrayItem(GetType(Dossier)), _
        Xml.Serialization.XmlArrayItem(GetType(Fichier))> _
        Public Items As New ArrayList

        Public Sub New()
            Me.Version = VERSION_COURANTE
        End Sub

#Region "Import/Export XML"
        Public Sub Enregistrer(ByVal fichier As String)
            Dim xser As New Xml.Serialization.XmlSerializer(GetType(ManifestSauvegarde))
            Dim sw As New IO.StreamWriter(fichier)
            xser.Serialize(sw, Me)
            sw.Close()
        End Sub

        Public Shared Function Charger(ByVal fichier As String) As ManifestSauvegarde
            Dim m As ManifestSauvegarde
            Dim xser As New Xml.Serialization.XmlSerializer(GetType(ManifestSauvegarde))
            Dim sr As New IO.StreamReader(fichier)
            Try
                m = CType(xser.Deserialize(sr), ManifestSauvegarde)
            Finally
                sr.Close()
            End Try
            Return m
        End Function
#End Region
    End Class

    Public Class ItemBackup
        <Xml.Serialization.XmlAttribute()> Public description As String

        Public Overrides Function ToString() As String
            Return description
        End Function
    End Class

    Public Class DatabaseBackup
        Inherits ItemBackup

        <Xml.Serialization.XmlAttribute()> Public fichierBackup As String
        <Xml.Serialization.XmlAttribute()> Public nomBase As String

        Public Sub New()

        End Sub

        Public Sub New(ByVal description As String, ByVal fichierBackup As String, ByVal nomBase As String)
            Me.New()
            Me.description = description
            Me.fichierBackup = fichierBackup
            Me.nomBase = nomBase
        End Sub
    End Class

    Public Class Dossier
        Inherits ItemBackup

        <Xml.Serialization.XmlAttribute()> Public nomDossier As String

        Public Sub New()

        End Sub

        Public Sub New(ByVal description As String, ByVal nomDossier As String)
            Me.New()
            Me.description = description
            Me.nomDossier = nomDossier
        End Sub
    End Class

    Public Class Fichier
        Inherits ItemBackup

        <Xml.Serialization.XmlAttribute()> Public nomFichier As String
        <Xml.Serialization.XmlAttribute()> Public sauvegarderFichierAvantRemplacement As Boolean = False

        Public Sub New()

        End Sub

        Public Sub New(ByVal description As String, ByVal nomFichier As String, ByVal sauvegarderFichierAvantRemplacement As Boolean)
            Me.New()
            Me.description = description
            Me.nomFichier = nomFichier
            Me.sauvegarderFichierAvantRemplacement = sauvegarderFichierAvantRemplacement
        End Sub
    End Class
#End Region

End Class
