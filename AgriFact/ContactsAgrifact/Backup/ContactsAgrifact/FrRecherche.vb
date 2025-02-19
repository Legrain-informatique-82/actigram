Public Class FrRecherche

#Region " Gestion des dossiers "
    Private Sub ChargerDossiers()
        My.Settings.NomDossier = Nothing
        Dim trouve As Boolean = False
        For Each dos As Dossier In My.Settings.Dossiers
            Dim mn As New ToolStripMenuItem(dos.Nom, Nothing, AddressOf ChangeDossier)
            mn.Tag = dos
            If dos.ConnString = My.Settings.ConnAgrifact Then
                trouve = True
                mn.Checked = True
                Me.Text = String.Format("Recherche sur le dossier {0}", dos.Nom)
                My.Settings.NomDossier = dos.Nom
            End If
            DossierToolStripMenuItem.DropDownItems.Insert(0, mn)
        Next
        If Not trouve Then
            Dim sb As New SqlClient.SqlConnectionStringBuilder(My.Settings.ConnAgrifact)
            Me.Text = String.Format("Recherche sur la base {0}\{1}", sb.DataSource, sb.InitialCatalog)
        End If
    End Sub

    Private Sub ChangeDossier(ByVal sender As Object, ByVal e As EventArgs)
        My.Settings.NomDossier = Nothing
        Dim mn As ToolStripMenuItem = Cast(Of ToolStripMenuItem)(sender)
        If mn.Tag Is Nothing OrElse Not TypeOf mn.Tag Is Dossier Then Exit Sub
        Cursor.Current = Cursors.WaitCursor
        Application.DoEvents()
        If SqlProxy.TestConnection(Cast(Of Dossier)(mn.Tag).ConnString) Then
            SqlProxy.SetDefaultConnection(Cast(Of Dossier)(mn.Tag).ConnString)
            ResetAdapters()
            For Each i As ToolStripMenuItem In DossierToolStripMenuItem.DropDownItems
                i.Checked = False
            Next
            mn.Checked = True
            Me.Text = String.Format("Recherche sur le dossier {0}", mn.Text)
            My.Settings.NomDossier = mn.Text
        End If
        Cursor.Current = Cursors.Default
    End Sub
#End Region

    Private Sub ResetAdapters()
        Me.RechercheTableAdapter = New DsAgrifactTableAdapters.RechercheTableAdapter
    End Sub

    Private bmpUser As Bitmap
    Private bmpUserAccounts As Bitmap
    Private Sub FrRecherche_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        bmpUser = My.Resources.user
        bmpUser.MakeTransparent(Color.Magenta)
        bmpUserAccounts = My.Resources.userAccounts
        bmpUserAccounts.MakeTransparent(Color.Magenta)
        lbStatus.Text = ""
        ApplyStyle(Me.dgResults)
        ChargerDossiers()
        CType(Me.BtGo.Image, Bitmap).MakeTransparent(Color.Magenta)
        Me.TxRecherche.Control.Select()
        Me.TxRecherche.SelectAll()
    End Sub

    Private Sub FrRecherche_Shown(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Shown
        Me.Hide()
    End Sub

    Private Sub FrRecherche_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        If e.CloseReason = CloseReason.UserClosing Then
            For Each f As Form In childrenToKill
                f.Close()
            Next
            childrenToKill.Clear()
            Me.Hide()
            Me.DsAgrifact.Recherche.Clear()
            e.Cancel = True
            GC.WaitForPendingFinalizers()
            GC.Collect() 'Pour essayer de r�cup�rer la m�moire en mode sommeil.
            SetProcessWorkingSetSize() 'Et encore.
        End If
    End Sub

    Private Declare Auto Function SetProcessWorkingSetSize Lib "kernel32.dll" (ByVal procHandle As IntPtr, ByVal min As Int32, ByVal max As Int32) As Boolean
    Friend Sub SetProcessWorkingSetSize()
        Try
            Dim Mem As Process = Process.GetCurrentProcess()
            SetProcessWorkingSetSize(Mem.Handle, -1, -1)
        Catch ex As Exception
        End Try
    End Sub


    Private Sub RechercherToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles RechercherToolStripMenuItem.Click
        Me.Show()
        If Me.WindowState = FormWindowState.Minimized Then Me.WindowState = FormWindowState.Normal
        If Not Me.ShowInTaskbar Then Me.ShowInTaskbar = True
        Me.TxRecherche.Control.Select()
    End Sub

    Private Sub QuitterToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles QuitterToolStripMenuItem.Click
        Application.Exit()
    End Sub

    Private Sub notIcon_DoubleClick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles notIcon.DoubleClick
        RechercherToolStripMenuItem.PerformClick()
    End Sub

    Private Sub TxRecherche_KeyPress(ByVal sender As Object, ByVal e As System.Windows.Forms.KeyPressEventArgs) Handles TxRecherche.KeyPress
        If e.KeyChar = vbCr Then
            BtGo.PerformButtonClick()
            e.Handled = True
        End If
    End Sub

    Private Sub BtGo_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtGo.ButtonClick
        Dim search As String = Me.TxRecherche.Text.Trim
        If Me.MotEntierToolStripMenuItem.Checked Then
            search = " " & search & " "
        End If
        Cursor.Current = Cursors.WaitCursor
        Application.DoEvents()
        Me.RechercheTableAdapter.SearchBy(Me.DsAgrifact.Recherche, search)
        Me.TxRecherche.SelectAll()
        If Me.RechercheBindingSource.Count = 0 Then
            Me.TxRecherche.Control.Select()
            lbStatus.Text = "Aucun r�sultat"
        Else
            Me.dgResults.Select()
            lbStatus.Text = String.Format("{0} r�sultats", Me.RechercheBindingSource.Count)
        End If
        Cursor.Current = Cursors.Default
    End Sub

    Private Sub dgResults_KeyPress(ByVal sender As Object, ByVal e As System.Windows.Forms.KeyPressEventArgs) Handles dgResults.KeyPress
        If e.KeyChar = vbCr Then
            dgResults_CellMouseDoubleClick(Nothing, Nothing)
            e.Handled = True
        End If
    End Sub

    Private Sub dgResults_CellMouseDoubleClick(ByVal sender As System.Object, ByVal e As System.Windows.Forms.DataGridViewCellMouseEventArgs) Handles dgResults.CellMouseDoubleClick
        If Me.RechercheBindingSource.Position < 0 Then Exit Sub
        Dim dr As DsAgrifact.RechercheRow = Cast(Of DsAgrifact.RechercheRow)(Cast(Of DataRowView)(Me.RechercheBindingSource.Current).Row)
        Select Case dr.type
            Case "E" : FrEntreprise.Show(CInt(dr.Cle), Me)
            Case "P" : FrPersonne.Show(CInt(dr.Cle), Me)
        End Select
    End Sub

    Private Sub ParametresToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ParametresToolStripMenuItem.Click
        Using fr As New FrParametres
            If fr.ShowDialog() = Windows.Forms.DialogResult.OK Then
                My.Settings.NomDossier = Nothing
                For Each i As ToolStripMenuItem In DossierToolStripMenuItem.DropDownItems
                    i.Checked = False
                Next
                Dim sb As New SqlClient.SqlConnectionStringBuilder(My.Settings.ConnAgrifact)
                Me.Text = String.Format("Recherche sur la base {0}\{1}", sb.DataSource, sb.InitialCatalog)
                ResetAdapters()
            End If
        End Using
    End Sub

    Private Sub FrRecherche_TextChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.TextChanged
        notIcon.Text = Me.Text
    End Sub

    Private Sub InterventionClientToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles InterventionClientToolStripMenuItem.Click
        Dim fr As New FrIntervention
        fr.Show()
    End Sub

    Private Sub dgResults_DataBindingComplete(ByVal sender As System.Object, ByVal e As System.Windows.Forms.DataGridViewBindingCompleteEventArgs) Handles dgResults.DataBindingComplete
        If e.ListChangedType = System.ComponentModel.ListChangedType.Reset Then
            For Each dr As DataGridViewRow In dgResults.Rows
                Select Case CType(dr.DataBoundItem, DataRowView)("Type").ToString
                    Case "P" : Cast(Of DataGridViewImageCell)(dr.Cells(Me.TypeIcon.Index)).Value = bmpUser
                    Case "E" : Cast(Of DataGridViewImageCell)(dr.Cells(Me.TypeIcon.Index)).Value = bmpUserAccounts
                End Select
            Next
        End If
    End Sub

    Private Sub dgResults_CellToolTipTextNeeded(ByVal sender As System.Object, ByVal e As System.Windows.Forms.DataGridViewCellToolTipTextNeededEventArgs) Handles dgResults.CellToolTipTextNeeded
        If e.ColumnIndex = Me.ColTel.Index Then
            If e.RowIndex < 0 Then Exit Sub
            Dim dr As DataGridViewRow = dgResults.Rows(e.RowIndex)
            If dr.DataBoundItem Is Nothing Then Exit Sub
            Dim sql As String
            Select Case CType(dr.DataBoundItem, DataRowView)("Type").ToString
                Case "P" : sql = SqlProxy.FormatSql("SELECT * FROM Telephone WHERE nPersonne={0}", CType(dr.DataBoundItem, DataRowView)("Cle"))
                Case "E" : sql = SqlProxy.FormatSql("SELECT * FROM TelephoneEntreprise WHERE nEntreprise={0}", CType(dr.DataBoundItem, DataRowView)("Cle"))
            End Select

            Using s As New SqlProxy(My.Settings.ConnAgrifact)
                Dim dt As DataTable = s.ExecuteDataTable(sql)
                Dim tb As New System.Text.StringBuilder
                For Each rw As DataRow In dt.Rows
                    tb.AppendLine(String.Format("{0}: {1}", rw("Type"), rw("Numero")))
                Next
                'e.ToolTipText = tb.ToString.Trim
                Dim r As Rectangle = dgResults.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, True)
                ShowTooltip(tb.ToString.Trim, dgResults.PointToScreen(New Point(r.Right - 10, r.Bottom - 10)))
            End Using
        End If
    End Sub

    Dim childrenToKill As New List(Of Form)
    Private Sub ShowTooltip(ByVal str As String, ByVal p As Point)
        For Each f As Form In childrenToKill
            f.Close()
        Next
        childrenToKill.Clear()
        If str.Length = 0 Then Exit Sub
        Dim mp As Point = Form.MousePosition
        For i As Integer = 0 To 5
            Application.DoEvents()
            System.Threading.Thread.Sleep(20)
        Next
        Dim mp2 As Point = Form.MousePosition
        If Not (Math.Abs(mp.X - mp2.X) < 10 AndAlso Math.Abs(mp.Y - mp2.Y) < 10) Then Exit Sub
        Dim fr As New FrToolTip
        fr.Text = str
        fr.Show()
        fr.Location = p
        childrenToKill.Add(fr)
    End Sub

    Private Sub MenuOpen_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MenuOpen.Click
        MenuOpen.DropDown = ctxIcon
        MenuOpen.ShowDropDown()
    End Sub
End Class
