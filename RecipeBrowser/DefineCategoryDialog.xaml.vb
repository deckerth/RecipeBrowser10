' Die Elementvorlage "Inhaltsdialog" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports Windows.Storage

Public NotInheritable Class DefineCategoryDialog
    Inherits ContentDialog

    Private originalCategory As RecipeFolder
    Private creationMode As Boolean

    Event LoadImageRequested(ByVal imageFile As Windows.Storage.StorageFile)

    Public Sub New(Optional originalCategoryName As String = Nothing)

        InitializeComponent()

        If originalCategoryName Is Nothing Then
            originalCategory = Nothing
        Else
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
            originalCategory = categories.GetFolder(originalCategoryName)
        End If

        creationMode = (originalCategory Is Nothing)

        AddHandler LoadImageRequested, AddressOf OnLoadImageRequested

        If Not creationMode Then
            CategoryName.Text = originalCategory.Name
            CategoryEditor.Title = App.Texts.GetString("EditCategoryTitle")
            RaiseEvent LoadImageRequested(originalCategory.ImageFile)
        End If
    End Sub

    Private SelectedImage As Windows.Storage.StorageFile

    Private Async Sub OnLoadImageRequested(ByVal imageFile As Windows.Storage.StorageFile)
        Await LoadImageAsync(imageFile)
    End Sub

    Private Async Function LoadImageAsync(ByVal imageFile As Windows.Storage.StorageFile) As Task

        If imageFile IsNot Nothing Then
            Try
                ' Open a stream for the selected file.
                Dim fileStream = Await imageFile.OpenAsync(Windows.Storage.FileAccessMode.Read)

                ' Set the image source to the selected bitmap.
                Dim BitmapImage = New Windows.UI.Xaml.Media.Imaging.BitmapImage()

                BitmapImage.SetSource(fileStream)
                CategoryImage.Source = BitmapImage
            Catch ex As Exception
            End Try
        End If

        SelectedImage = imageFile

    End Function

    Private Async Sub LoadCategoryImage_Click(sender As Object, e As RoutedEventArgs)

        Dim openPicker = New Windows.Storage.Pickers.FileOpenPicker()
        openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail

        ' Filter to include a sample subset of file types.
        openPicker.FileTypeFilter.Clear()
        openPicker.FileTypeFilter.Add(".png")
        openPicker.FileTypeFilter.Add(".jpg")

        ' Open the file picker.
        Dim file = Await openPicker.PickSingleFileAsync()

        ' file is null if user cancels the file picker.
        If file IsNot Nothing Then
            Await LoadImageAsync(file)

            If CategoryEditor.IsPrimaryButtonEnabled = False AndAlso CategoryNameIsValid() Then
                CategoryEditor.IsPrimaryButtonEnabled = True
            End If
        End If
    End Sub

    Class StringContainer
        Public content As New String("")
    End Class

    Private Function CategoryNameIsValid(Optional ByRef errorMessage As StringContainer = Nothing) As Boolean

        If CategoryName.Text Is Nothing OrElse CategoryName.Text.Trim().Equals("") Then
            If errorMessage IsNot Nothing Then
                errorMessage.content = App.Texts.GetString("CategoryNameIsEmpty")
            End If
            Return False
        End If

        If creationMode OrElse Not CategoryName.Text.Trim().Equals(originalCategory.Name) Then
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

            If categories.GetFolder(CategoryName.Text.Trim()) IsNot Nothing Then
                If errorMessage IsNot Nothing Then
                    errorMessage.content = App.Texts.GetString("CategoryDoesAlreadyExist")
                End If
                Return False
            End If
        End If

        If errorMessage IsNot Nothing Then
            errorMessage.content = ""
        End If
        Return True

    End Function

    Private Async Sub SaveButtonClick(sender As ContentDialog, args As ContentDialogButtonClickEventArgs)

        Dim errorMessage As New StringContainer()

        If CategoryNameIsValid(errorMessage) Then
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
            If creationMode Then
                Await categories.CreateCategoryAsync(CategoryName.Text.Trim(), SelectedImage)
            Else
                Await categories.ModifyCategoryAsync(originalCategory, CategoryName.Text.Trim(), SelectedImage)
            End If
            CategoryEditor.Hide()
        Else
            Dim messageDialog = New Windows.UI.Popups.MessageDialog(errorMessage.content)
            Await messageDialog.ShowAsync()
            Return
        End If

    End Sub

    Private saveNecessary As Boolean

    Private Sub CategoryName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles CategoryName.TextChanged

        Dim errorMessage As New StringContainer()

        If CategoryNameIsValid(errorMessage) Then
            CategoryName.SetValue(BorderBrushProperty, New SolidColorBrush(Windows.UI.Colors.Black))
            CategoryEditor.IsPrimaryButtonEnabled = True
        Else
            CategoryName.SetValue(BorderBrushProperty, New SolidColorBrush(Windows.UI.Colors.Red))
            CategoryEditor.IsPrimaryButtonEnabled = False
        End If

        ErrorMessageDisplay.Text = errorMessage.content

        saveNecessary = True
    End Sub


    Private Sub CancelButtonClick(sender As ContentDialog, args As ContentDialogButtonClickEventArgs)

        CategoryEditor.Hide()

    End Sub
End Class
