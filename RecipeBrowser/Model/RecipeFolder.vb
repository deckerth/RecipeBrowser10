Imports RecipeBrowser.Recipe
Imports Windows.Storage
Imports Windows.Storage.Search

Public Class RecipeFolder

#Region "Properties"
    Public Property Name As String
    Public Property Folder As Windows.Storage.StorageFolder
    Public Property Image As BitmapImage
    Public Property ImageFile As Windows.Storage.StorageFile

    Public Enum SortOrder
        ByNameAscending
        ByDateDescending
        ByLastCookedDescending
        NoSorting
    End Enum

    Protected _Recipes As New ObservableCollection(Of Recipe)()

    Public ReadOnly Property Recipes As ObservableCollection(Of Recipe)
        Get
            Return _Recipes
        End Get
    End Property

    Protected _GroupedRecipes As New ObservableCollection(Of RecipesGroup)()
    Public ReadOnly Property GroupedRecipes As ObservableCollection(Of RecipesGroup)
        Get
            Return _GroupedRecipes
        End Get
    End Property

    Protected _ContentLoaded As Boolean
    Public Property ContentIsGrouped As Boolean
    Public Function ContentLoaded() As Boolean
        Return _ContentLoaded
    End Function
#End Region

#Region "Sorting"
    Protected _SortOrder As SortOrder = SortOrder.ByNameAscending
    Protected _RecipeList As New List(Of Recipe)

    Public Sub SetSortOrder(ByVal order As SortOrder)
        If order <> _SortOrder Then
            _SortOrder = order
            ApplySortOrder()
        End If
    End Sub
#End Region

#Region "Load Recipe"
    Private Function GetFolder(ByRef file As Windows.Storage.StorageFile) As String

        ' Example for a path: Path = "G:\Users\Thomas\SkyDrive\Rezepte\Auflauf\Kohlrabi-Lasagne mit Spinat und Tomaten.pdf"

        Dim pos = file.Path.LastIndexOf("\")

        If pos = -1 Then
            Return file.Path
        End If

        Dim Folder = file.Path.Substring(0, pos)

        Return Folder

    End Function

    Private Function GetCategory(ByRef file As Windows.Storage.StorageFile) As String

        ' Example for a folder: Path = "G:\Users\Thomas\SkyDrive\Rezepte\Auflauf"

        Dim folderName As String = GetFolder(file)
        Dim category As String

        Dim pos = folderName.LastIndexOf("\")
        If pos <> -1 Then
            category = folderName.Substring(pos + 1)
        Else
            category = folderName
        End If

        Return category

    End Function

    Async Function LoadRecipeAsync(file As Windows.Storage.StorageFile, loadMetadata As Boolean, checkForNotes As Boolean) As Task(Of Recipe)

        Dim _recipe = New Recipe
        Dim metaDataFile As Windows.Storage.StorageFile

        _recipe.Name = file.Name.Remove(file.Name.Length - 4)  ' delete suffix .pdf

        If Name = SearchResults.FolderName Then
            _recipe.Category = GetCategory(file)

            If _recipe.Category Is Nothing Then
                Return Nothing
            End If
            Try
                Dim parent As StorageFolder

                If checkForNotes Or loadMetadata Then
                    parent = RecipeFolders.GetInstance().GetFolder(_recipe.Category).Folder
                End If
                If checkForNotes And parent IsNot Nothing Then
                    _recipe.Notes = TryCast(Await parent.TryGetItemAsync(_recipe.Name + ".rtf"), Windows.Storage.StorageFile)
                End If
                If loadMetadata And parent IsNot Nothing Then
                    metaDataFile = TryCast(Await parent.TryGetItemAsync(_recipe.Name + ".xml"), Windows.Storage.StorageFile)
                End If
            Catch ex As Exception
                App.Logger.Write("Unable to access parent of: " + file.Path + ": " + ex.ToString)
            End Try
        Else
            _recipe.Category = Name
            Try
                If checkForNotes Then
                    _recipe.Notes = Await Folder.GetFileAsync(_recipe.Name + ".rtf")
                End If
            Catch ex As Exception
            End Try
            Try
                If loadMetadata Then
                    metaDataFile = Await Folder.GetFileAsync(_recipe.Name + ".xml")
                End If
            Catch ex As Exception
            End Try
        End If

        Dim properties = Await file.GetBasicPropertiesAsync()
        _recipe.CreationDateTime = properties.ItemDate.DateTime
        _recipe.File = file

        If metaDataFile IsNot Nothing Then
            Await RecipeMetadata.Instance.ReadMetadataAsync(_recipe, metaDataFile)
        End If

        _recipe.RenderSubTitle()

        Return _recipe

    End Function

#End Region

#Region "Load folder content"
    Protected Async Function SetUpFolderFromFileListAsync(fileList As IReadOnlyList(Of Windows.Storage.StorageFile)) As Task

        ' This method is used by the original folders and the search folder.
        _Recipes.Clear()
        _RecipeList.Clear()

        If fileList Is Nothing Then
            Return
        End If

        Dim rtfFiles As New List(Of Windows.Storage.StorageFile)
        Dim xmlFiles As New List(Of Windows.Storage.StorageFile)

        For Each aFile In fileList
            If aFile.Name.ToUpper.EndsWith(".PDF") Then
                Try
                    Dim _recipe = Await LoadRecipeAsync(aFile, loadMetadata:=Name = SearchResults.FolderName, checkForNotes:=Name = SearchResults.FolderName)
                    _RecipeList.Add(_recipe)
                Catch ex As Exception
                    App.Logger.Write("Recipe cannot be loaded: " + aFile.Path + ex.ToString)
                End Try
            ElseIf aFile.Name.ToUpper.EndsWith(".RTF") Then
                rtfFiles.Add(aFile) ' Search folder: Add the recipe to the search result; Normal folder: Log the file in the recipe data
            ElseIf aFile.Name.ToUpper.EndsWith(".XML") Then
                If Name <> SearchResults.FolderName Then
                    xmlFiles.Add(aFile) ' use the file instance later in order to load the metadata
                End If
            Else
                App.Logger.Write("Unsupported File: " + aFile.Path + aFile.ContentType)
            End If
        Next

        ' If the search expression has been found in a note file, try to add the corresponding recipe to the search result list
        For Each aFile In rtfFiles
            Dim recipe = aFile.Name.Remove(aFile.Name.Length - 4)  ' delete suffix e.g. ".rtf"
            If Name = SearchResults.FolderName Then
                Dim category = GetCategory(aFile)
                If GetRecipe(category, recipe) Is Nothing Then
                    Try ' Add the recipe to the search result
                        Dim parent = RecipeFolders.GetInstance().GetFolder(category).Folder
                        Dim recipeFile = Await parent.GetFileAsync(recipe + ".pdf")
                        Dim _recipe = Await LoadRecipeAsync(recipeFile, loadMetadata:=True, checkForNotes:=False)
                        _recipe.Notes = aFile
                        _RecipeList.Add(_recipe)
                    Catch ex As Exception
                        App.Logger.Write("Unable to add recipe to search result: " + recipe + ": " + ex.ToString)
                    End Try
                End If
            Else
                SetNote(Name, recipe, aFile)
            End If
        Next

        For Each aFile In xmlFiles
            Dim recipeName = aFile.Name.Remove(aFile.Name.Length - 4)  ' delete suffix .xml
            Dim _recipe = GetRecipe(Name, recipeName)
            If _recipe IsNot Nothing Then
                Await RecipeMetadata.Instance.ReadMetadataAsync(_recipe, aFile)
            End If
        Next

        ApplySortOrder()

        _ContentLoaded = True

    End Function


    Public Overridable Async Function LoadAsync() As Task

        _Recipes.Clear()
        _RecipeList.Clear()

        Dim files As IReadOnlyList(Of StorageFile)

        Try
            files = Await Folder.GetFilesAsync()
        Catch ex As Exception
        End Try

        If files Is Nothing Then
            App.Logger.Write("Folder content cannot be read: " + Folder.DisplayName)
        ElseIf files.Count = 0 Then
            App.Logger.Write("Folder is empty: " + Folder.DisplayName)
        End If

        Await SetUpFolderFromFileListAsync(files)

    End Function

    Protected Sub ApplySortOrder()
        Dim _comparer As IComparer(Of Recipe)
        Select Case _SortOrder
            Case SortOrder.ByNameAscending
                _comparer = New RecipeComparer_NameAscending
            Case SortOrder.ByDateDescending
                _comparer = New RecipeComparer_DateDescending
            Case SortOrder.ByLastCookedDescending
                _comparer = New RecipeComparer_LastCookedDescending
        End Select
        If _comparer IsNot Nothing Then
            _RecipeList.Sort(_comparer)
        End If
        _Recipes.Clear()

        For Each item In _RecipeList
            _Recipes.Add(item)
        Next
    End Sub

    Sub Invalidate()

        _RecipeList.Clear()
        _Recipes.Clear()
        _GroupedRecipes.Clear()
        _ContentLoaded = False

    End Sub

#End Region

#Region "Access recipes"
    Public Function GetRecipe(category As String, title As String) As Recipe

        Dim matches = _RecipeList.Where(Function(otherRecipe) otherRecipe.Name.Equals(title) And otherRecipe.Category.Equals(category))
        If matches.Count() > 0 Then
            Return matches.First()
        End If
        Return Nothing

    End Function

    Public Async Function GetRecipeAsync(category As String, title As String) As Task(Of Recipe)
        If ContentLoaded() Then
            Return GetRecipe(category, title)
        Else
            Dim file As Windows.Storage.StorageFile
            Try
                file = Await Folder.GetFileAsync(title + ".pdf")
            Catch ex As Exception
                Return Nothing
            End Try
            If file IsNot Nothing Then
                Return Await LoadRecipeAsync(file, loadMetadata:=True, checkForNotes:=True)
            End If
        End If

        Return Nothing
    End Function

#End Region

#Region "Search recipes"
    Public Function GetMatchingRecipes(searchString As String) As IEnumerable(Of Recipe)

        Return Recipes.Where(Function(otherRecipe) otherRecipe.Name.IndexOf(searchString, StringComparison.CurrentCultureIgnoreCase) > -1 And otherRecipe.ItemType <> ItemTypes.Header)

    End Function
#End Region

#Region "Delete recipe"
    Public Overridable Function DeleteRecipe(ByRef recipeToDelete As Recipe) As Boolean

        If Not ContentLoaded() Then
            Return False
        End If

        If _RecipeList.Contains(recipeToDelete) Then
            _Recipes.Remove(recipeToDelete)
            _RecipeList.Remove(recipeToDelete)
            Return True
        Else
            Dim copy = GetRecipe(recipeToDelete.Category, recipeToDelete.Name)
            If copy IsNot Nothing Then
                Return DeleteRecipe(copy)
            End If
        End If
        Return False

    End Function

#End Region

#Region "LastCooked"
    Public Sub UpdateStatistics(changedRecipe As Recipe)

        Dim recipe = GetRecipe(changedRecipe.Category, changedRecipe.Name)

        If recipe IsNot Nothing Then
            recipe.LastCooked = changedRecipe.LastCooked
            recipe.CookedNoOfTimes = changedRecipe.CookedNoOfTimes
            recipe.RenderSubTitle()
        End If

    End Sub

#End Region

#Region "Notes"
    Public Sub SetNote(category As String, title As String, note As Windows.Storage.StorageFile)

        Dim recipe = GetRecipe(category, title)

        If recipe IsNot Nothing Then
            recipe.Notes = note
        End If

    End Sub

    Public Sub UpdateNote(changedRecipe As Recipe)

        Dim recipe = GetRecipe(changedRecipe.Category, changedRecipe.Name)

        If recipe IsNot Nothing AndAlso Not Object.ReferenceEquals(recipe, changedRecipe) Then
            recipe.Notes = changedRecipe.Notes
        End If
    End Sub

#End Region

#Region "Images"

    Private Async Function LoadImageAsync(ByVal imageFile As Windows.Storage.StorageFile) As Task(Of BitmapImage)

        If imageFile IsNot Nothing Then
            Try
                ' Open a stream for the selected file.
                Dim fileStream = Await imageFile.OpenAsync(Windows.Storage.FileAccessMode.Read)

                ' Set the image source to the selected bitmap.
                Dim bitmap = New Windows.UI.Xaml.Media.Imaging.BitmapImage()

                bitmap.SetSource(fileStream)
                Return bitmap
            Catch ex As Exception
            End Try
        End If

        Return Nothing
    End Function

    Public Async Function GetImageFilesOfRecipeAsync(aRecipe As Recipe) As Task(Of IReadOnlyList(Of StorageFile))


        If Folder Is Nothing Then
            Return Nothing
        End If

        Dim fileTypeFilter As New List(Of String)
        fileTypeFilter.Add(".jpg")
        fileTypeFilter.Add(".png")

        Dim queryOptions As New QueryOptions(CommonFileQuery.OrderByName, fileTypeFilter)
        queryOptions.FolderDepth = FolderDepth.Shallow
        queryOptions.ApplicationSearchFilter = aRecipe.Name + "_image_*"

        Dim queryResult As StorageFileQueryResult = Folder.CreateFileQueryWithOptions(queryOptions)
        Dim files As IReadOnlyList(Of StorageFile) = Await queryResult.GetFilesAsync()

        Return files

    End Function

    Public Async Function GetImagesOfRecipeAsync(aRecipe As Recipe) As Task
        If Folder Is Nothing Then
            Return
        End If

        Dim files As IReadOnlyList(Of StorageFile) = Await GetImageFilesOfRecipeAsync(aRecipe)
        If files Is Nothing Then
            Return
        End If

        aRecipe.Pictures = New ObservableCollection(Of RecipeImage)
        For Each aFile In files
            Dim image = Await LoadImageAsync(aFile)
            If image IsNot Nothing Then
                aRecipe.Pictures.Add(New RecipeImage(aFile, image))
            End If
        Next
    End Function
#End Region

#Region "RenameRecipe"

    Public Overridable Sub RenameRecipe(ByRef recipeToRename As Recipe, ByVal oldName As String, ByVal newName As String)
        recipeToRename.Name = newName

        If ContentLoaded() AndAlso Not _RecipeList.Contains(recipeToRename) Then
            Dim copy = GetRecipe(recipeToRename.Category, oldName)
            If copy IsNot Nothing Then
                copy.Name = newName
            End If
        End If

    End Sub

#End Region

#Region "ChangeCategory"

    Public Overridable Sub ChangeCategory(ByRef recipeToChange As Recipe, ByRef oldCategpry As String, ByRef newCategory As String)
        recipeToChange.Category = newCategory
        recipeToChange.RenderSubTitle()

        If ContentLoaded() AndAlso Not _RecipeList.Contains(recipeToChange) Then
            Dim copy = GetRecipe(oldCategpry, recipeToChange.Name)
            If copy IsNot Nothing Then
                copy.Category = newCategory
                copy.RenderSubTitle()
            End If
        End If
    End Sub

#End Region
End Class
