' Die Elementvorlage "Inhaltsdialog" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Public NotInheritable Class RecipeFromExternalSource
    Inherits ContentDialog

    Public Property CategoryList As New List(Of String)

    Public Sub New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.

        For Each folder In RecipeFolders.Current.Folders
            CategoryList.Add(folder.Name)
        Next
    End Sub

    Private Sub ContentDialog_PrimaryButtonClick(sender As ContentDialog, args As ContentDialogButtonClickEventArgs)

        Persistency.CookingHistory.Current.LogRecipeCooked(ExternalRecipeCategory.SelectedItem, ExternalRecipeTitle.Text, ExternalRecipeDate.Date.Date, ExternalRecipeSource.Text)

    End Sub

    Private Sub ContentDialog_SecondaryButtonClick(sender As ContentDialog, args As ContentDialogButtonClickEventArgs)

    End Sub

    Private Sub ExternalRecipeTitle_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ExternalRecipeTitle.TextChanged
        AddExternalRecipeDialog.IsPrimaryButtonEnabled = ExternalRecipeTitle.Text.Length > 0
    End Sub
End Class
