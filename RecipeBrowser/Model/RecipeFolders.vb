Imports Windows.UI.Popups
Imports Windows.Storage
Imports Windows.Storage.Provider

Public Class RecipeFolders

    Private _Folders As New ObservableCollection(Of RecipeFolder)()
    Public ReadOnly Property Folders As ObservableCollection(Of RecipeFolder)
        Get
            Return _Folders
        End Get
    End Property

    Public Property FavoriteFolder As Favorites
    Public Property SearchResultsFolder As SearchResults

    Private initialized As Boolean

    Public Function ContentLoaded() As Boolean
        Return initialized
    End Function

    Private rootFolder As Windows.Storage.StorageFolder

    Public Shared Function GetInstance() As RecipeFolders
        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
        Return categories
    End Function

    Public Async Function GetFolderAsync(name As String) As Task(Of RecipeFolder)

        Dim folder = GetFolder(name)

        If folder IsNot Nothing AndAlso Not folder.ContentLoaded Then
            Await folder.LoadAsync()
        End If

        Return folder

    End Function


    Public Function GetFolder(name As String) As RecipeFolder

        If name = FavoriteFolder.Name Then
            Return FavoriteFolder
        ElseIf name = SearchResults.FolderName Then
            Return SearchResultsFolder
        Else
            Dim matches = _Folders.Where(Function(otherFolder) otherFolder.Name.Equals(name))
            If matches.Count() = 1 Then
                Dim folder = matches.First()
                Return folder
            End If
        End If

        Return Nothing

    End Function

    Public Async Function GetRootFolderAsync() As Task(Of Windows.Storage.StorageFolder)

        Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings

        Dim mruToken = localSettings.Values("RootFolder")

        If String.IsNullOrEmpty(mruToken) Then
            Return Nothing
        Else
            Try
                Return Await Windows.Storage.AccessCache.StorageApplicationPermissions.MostRecentlyUsedList.GetFolderAsync(mruToken)
            Catch ex As Exception
                Return Nothing
            End Try
        End If

    End Function


    Public Async Function GetStorageFolderAsync(name As String) As Task(Of Windows.Storage.StorageFolder)

        If name.Equals("") Then
            Dim recipes = rootFolder
            Return recipes
        Else
            Dim matches = _Folders.Where(Function(otherFolder) otherFolder.Name.Equals(name))
            If matches.Count() = 1 Then
                Dim folder = matches.First()
                If Not folder.ContentLoaded Then
                    Await folder.LoadAsync()
                End If
                Return folder.Folder
            End If
        End If

        Return Nothing

    End Function

    Public Async Function GetRecipeAsync(mainCategory As String, category As String, title As String) As Task(Of Recipe)

        Dim folder = GetFolder(mainCategory)
        If folder IsNot Nothing Then
            Return Await folder.GetRecipeAsync(category, title)
        End If
        Return Nothing

    End Function

    Public Async Function LoadAsync() As Task

        rootFolder = Await GetRootFolderAsync()

        If rootFolder Is Nothing Then
            Return
        End If

        Dim images As StorageFolder
        Dim folders As IReadOnlyList(Of StorageFolder)

        Try
            images = Await rootFolder.GetFolderAsync("_folders")
        Catch ex As Exception
            App.Logger.Write("Unable to open the image folder:" + ex.ToString)
        End Try

        Try
            folders = Await rootFolder.GetFoldersAsync()
        Catch ex As Exception
            App.Logger.Write("Unable to open the root folder:" + ex.ToString)
        End Try

        ' Remove all temporary files
        Try
            Dim tempFolder = Windows.Storage.ApplicationData.Current.LocalFolder
            Dim files = Await tempFolder.GetFilesAsync()
            For Each file In files
                If file.Name.ToUpper.EndsWith(".PNG") Then
                    Await file.DeleteAsync()
                End If
            Next
        Catch ex As Exception
            App.Logger.Write("An exception occurred while removing temporary files:" + ex.ToString)
        End Try

        _Folders.Clear()

        For Each folder In folders
            If Not folder.DisplayName.StartsWith("_") Then
                Dim category = New RecipeFolder

                category.Name = folder.DisplayName
                category.Folder = folder

                If images IsNot Nothing Then
                    Try
                        Dim categoryImage = Await images.GetFileAsync(folder.DisplayName + ".png")
                        If categoryImage IsNot Nothing Then
                            ' Open a stream for the selected file.
                            Dim fileStream = Await categoryImage.OpenAsync(Windows.Storage.FileAccessMode.Read)
                            ' Set the image source to the selected bitmap.
                            category.Image = New Windows.UI.Xaml.Media.Imaging.BitmapImage()
                            Await category.Image.SetSourceAsync(fileStream)
                            category.ImageFile = categoryImage
                        End If
                    Catch ex As Exception
                        App.Logger.Write("Error occurred while loading image for " + folder.DisplayName + ": " + ex.ToString)
                    End Try
                End If

                _Folders.Add(category)
            End If
        Next

        FavoriteFolder = New Favorites
        SearchResultsFolder = New SearchResults

        initialized = True
    End Function


    Public Async Function UpdateStatisticsAsync(changedRecipe As Recipe) As Task

        Dim folder = GetFolder(changedRecipe.Categegory)

        Await RecipeMetadata.Instance.WriteMetadataAsync(folder.Folder, changedRecipe)

        folder.UpdateStatistics(changedRecipe)

        SearchResultsFolder.UpdateStatistics(changedRecipe)
        FavoriteFolder.UpdateStatistics(changedRecipe)

    End Function


    Public Sub UpdateNote(changedRecipe As Recipe)

        Dim folder = GetFolder(changedRecipe.Categegory)

        folder.UpdateNote(changedRecipe)

        SearchResultsFolder.UpdateNote(changedRecipe)
        FavoriteFolder.UpdateNote(changedRecipe)

    End Sub

    Async Function ChangeRootFolder() As Task

        Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings

        Dim mruToken = localSettings.Values("RootFolder")
        Dim folder As Windows.Storage.StorageFolder

        Dim openPicker = New Windows.Storage.Pickers.FolderPicker()
        openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        openPicker.CommitButtonText = App.Texts.GetString("OpenRootFolder")
        openPicker.FileTypeFilter.Clear()
        openPicker.FileTypeFilter.Add("*")
        openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail
        folder = Await openPicker.PickSingleFolderAsync()
        If folder IsNot Nothing Then
            ' Add picked file to MostRecentlyUsedList.
            mruToken = Windows.Storage.AccessCache.StorageApplicationPermissions.MostRecentlyUsedList.Add(folder)
            localSettings.Values("RootFolder") = mruToken
        Else
            Return
        End If

        Await LoadAsync()

        If initialized Then
            SearchResultsFolder.Clear()
        End If

    End Function

    Async Function DeleteRecipeAsync(recipe As Recipe) As Task

        Dim failed As Boolean

        Try
            Await recipe.File.DeleteAsync()
        Catch ex As Exception
            failed = True
        End Try

        If failed Then
            Dim dialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToDelete"))
            Await dialog.ShowAsync()
            Return
        End If

        Dim folder = GetFolder(recipe.Categegory)

        Try
            If recipe.Notes IsNot Nothing Then
                Await recipe.Notes.DeleteAsync()
            End If
        Catch ex As Exception
        End Try

        Try
            Dim metadata = Await folder.Folder.GetFileAsync(recipe.Name + ".xml")
            Await metadata.DeleteAsync()
        Catch ex As Exception
        End Try

        folder.DeleteRecipe(recipe)
        If Not FavoriteFolder.ContentLoaded Then
            Await FavoriteFolder.LoadAsync()
        End If
        FavoriteFolder.DeleteRecipe(recipe)
        SearchResultsFolder.DeleteRecipe(recipe)

    End Function


    Public Async Function ChangeCategoryAsync(ByVal recipeToChange As Recipe, ByVal destinationCategory As RecipeFolder) As Task

        If recipeToChange.Categegory = destinationCategory.Name Then
            Return
        End If

        Dim errorFlag As Boolean
        Dim srcFolder As RecipeFolder

        Try
            Await recipeToChange.File.MoveAsync(destinationCategory.Folder, recipeToChange.File.Name, Windows.Storage.NameCollisionOption.GenerateUniqueName)

            If destinationCategory.ContentLoaded Then
                destinationCategory.Invalidate()
            End If
            srcFolder = GetFolder(recipeToChange.Categegory)
            If srcFolder.ContentLoaded Then
                srcFolder.DeleteRecipe(recipeToChange)
            End If

        Catch ex As Exception
            errorFlag = True
        End Try

        If errorFlag Or srcFolder Is Nothing Then
            Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToEditCategory"))
            Await messageDialog.ShowAsync()
        Else
            ' Move notes
            Try
                If recipeToChange.Notes IsNot Nothing Then
                    Await recipeToChange.Notes.MoveAsync(destinationCategory.Folder, recipeToChange.Notes.Name, Windows.Storage.NameCollisionOption.GenerateUniqueName)
                End If
            Catch ex As Exception
                errorFlag = True
            End Try

            'Move metadata if they exist
            Try
                Dim item = Await srcFolder.Folder.TryGetItemAsync(recipeToChange.Name + ".xml")
                If item IsNot Nothing Then
                    Dim metadataFile As Windows.Storage.StorageFile = TryCast(item, Windows.Storage.StorageFile)
                    If metadataFile IsNot Nothing Then
                        Await metadataFile.MoveAsync(destinationCategory.Folder, metadataFile.Name, Windows.Storage.NameCollisionOption.GenerateUniqueName)
                    End If
                End If
            Catch ex As Exception
            End Try

            FavoriteFolder.ChangeCategory(recipeToChange, destinationCategory.Name)
            SearchResultsFolder.ChangeCategory(recipeToChange, destinationCategory.Name)

            recipeToChange.Categegory = destinationCategory.Name
            recipeToChange.RenderSubTitle()
        End If

    End Function


    Public Async Function ModifyCategoryAsync(ByVal originalCategory As RecipeFolder, ByVal newCategoryName As String, ByVal categoryImageFile As Windows.Storage.StorageFile) As Task

        Dim errorFlag As Boolean
        Dim reload As Boolean

        Try
            If Not originalCategory.Name.Equals(newCategoryName) Then
                Await originalCategory.Folder.RenameAsync(newCategoryName)
                FavoriteFolder.RenameCategory(originalCategory.Name, newCategoryName)
                reload = True
            End If

            If categoryImageFile IsNot Nothing Then
                If originalCategory.ImageFile IsNot Nothing AndAlso originalCategory.ImageFile.Path.Equals(categoryImageFile.Path) Then
                    Await categoryImageFile.RenameAsync(newCategoryName + ".png")
                Else
                    Await CopyCategoryImage(newCategoryName, categoryImageFile)
                End If
                reload = True
            End If

            If reload Then
                Await LoadAsync()
            End If
        Catch ex As Exception
            errorFlag = True
        End Try

        If errorFlag Then
            Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToEditCategory"))
            Await messageDialog.ShowAsync()
        End If

    End Function

    Private Async Function CopyCategoryImage(ByVal newCategoryName As String, ByVal categoryImageFile As Windows.Storage.StorageFile) As Task

        If categoryImageFile IsNot Nothing Then
            Dim images As Windows.Storage.StorageFolder
            Try
                images = Await rootFolder.GetFolderAsync("_folders")
            Catch ex As Exception
            End Try
            If images Is Nothing Then
                images = Await rootFolder.CreateFolderAsync("_folders")
            End If

            Await categoryImageFile.CopyAsync(images, newCategoryName + ".png", Windows.Storage.NameCollisionOption.ReplaceExisting)
        End If

    End Function

    Public Async Function CreateCategoryAsync(ByVal newCategoryName As String, ByVal categoryImageFile As Windows.Storage.StorageFile) As Task

        Dim errorFlag As Boolean

        Try
            Await rootFolder.CreateFolderAsync(newCategoryName)

            Await CopyCategoryImage(newCategoryName, categoryImageFile)

            Await LoadAsync()
        Catch ex As Exception
            errorFlag = True
        End Try

        If errorFlag Then
            Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToCreateCategory"))
            Await messageDialog.ShowAsync()
        End If

    End Function

End Class
