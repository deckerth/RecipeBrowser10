' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class StartPage
    Inherits Page

    Private Async Sub ChooseRootFolder_Click(sender As Object, e As RoutedEventArgs) Handles StartButton.Click

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        Await categories.ChangeRootFolder()

        If categories IsNot Nothing AndAlso categories.ContentLoaded Then
            Me.Frame.Navigate(GetType(CategoryOverview))
        End If

    End Sub
End Class
