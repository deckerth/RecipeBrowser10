Imports Windows.Storage

Public Class Favorites
    Inherits RecipeFolder

    Public Shared FolderName As String = App.Texts.GetString("FavoritesFolder")

    Class RecipeDescriptor
        Public Category As String
        Public Name As String
    End Class

    Private FavoritesList As List(Of RecipeDescriptor)

    Public Sub New()
        'FolderName = App.Texts.GetString("FavoritesFolder")
        Me.Name = FolderName
    End Sub

    Private Sub LoadFavorites()

        If FavoritesList IsNot Nothing Then
            Return
        End If

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        Dim recipeList = roamingSettings.CreateContainer("Favorites", Windows.Storage.ApplicationDataCreateDisposition.Always)

        FavoritesList = New List(Of RecipeDescriptor)

        For Each item In recipeList.Values
            Dim recipeComposite As ApplicationDataCompositeValue = item.Value
            Dim folder As String
            Dim recipe As String
            Try
                folder = recipeComposite("Folder")
                recipe = recipeComposite("Recipe")
            Catch ex As Exception
            End Try
            If folder IsNot Nothing AndAlso recipe IsNot Nothing Then
                If folder.Equals(SearchResults.FolderName) Then
                    ' Delete this entry
                    recipeList.Values.Remove(item.Key)
                Else
                    Dim newRecipe As New RecipeDescriptor
                    newRecipe.Category = folder
                    newRecipe.Name = recipe
                    FavoritesList.Add(newRecipe)
                End If
            End If
        Next

    End Sub

    Public Async Function AddRecipeAsync(ByVal newRecipe As Recipe) As Task
        If Not _ContentLoaded Then
            Await LoadAsync()
        End If
        If GetRecipe(newRecipe.Categegory, newRecipe.Name) Is Nothing Then
            _RecipeList.Add(newRecipe)
            ApplySortOrder()

            Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
            Dim recipeList = roamingSettings.CreateContainer("Favorites", Windows.Storage.ApplicationDataCreateDisposition.Always)
            Dim recipeComposite = New Windows.Storage.ApplicationDataCompositeValue()
            recipeComposite("Folder") = newRecipe.Categegory
            recipeComposite("Recipe") = newRecipe.Name
            recipeList.Values(Guid.NewGuid().ToString()) = recipeComposite
        End If
    End Function

    Public Overrides Function DeleteRecipe(ByRef recipeToDelete As Recipe) As Boolean

        If Not MyBase.DeleteRecipe(recipeToDelete) Then
            Return False
        End If

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        Dim recipeList As ApplicationDataContainer = roamingSettings.CreateContainer("Favorites", Windows.Storage.ApplicationDataCreateDisposition.Always)
        Dim index As String
        For Each item In recipeList.Values
            Dim recipeComposite As ApplicationDataCompositeValue = item.Value
            If recipeComposite("Recipe").Equals(recipeToDelete.Name) Then
                index = item.Key
                Exit For
            End If
        Next
        If index IsNot Nothing Then
            recipeList.Values.Remove(index)
        End If

        Return True
    End Function

    Public Overrides Async Function LoadAsync() As Task

        Dim allFolders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        If FavoritesList Is Nothing Then
            LoadFavorites()
        End If

        _RecipeList.Clear()

        For Each item In FavoritesList
            Dim newRecipe As New Recipe

            newRecipe = Await allFolders.GetRecipeAsync(item.Category, item.Category, item.Name)
            If newRecipe IsNot Nothing Then
                _RecipeList.Add(newRecipe)
            End If
        Next

        ApplySortOrder()

        _ContentLoaded = True
    End Function

    Public Function IsFavorite(recipeToLookup As Recipe) As Boolean

        If ContentLoaded() Then
            Return GetRecipe(recipeToLookup.Categegory, recipeToLookup.Name) IsNot Nothing
        Else
            If FavoritesList Is Nothing Then
                LoadFavorites()
            End If

            Dim matches = FavoritesList.Where(Function(otherRecipe) otherRecipe.Name.Equals(recipeToLookup.Name) And otherRecipe.Category.Equals(recipeToLookup.Categegory))
            Return matches.Count() = 1
        End If

    End Function

    Public Sub RenameCategory(ByRef OldName As String, ByRef NewName As String)

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        Dim storedRecords = roamingSettings.CreateContainer("Favorites", Windows.Storage.ApplicationDataCreateDisposition.Always)
        Dim toRename As New List(Of String)

        For Each item In storedRecords.Values
            If item.Value("Folder") = OldName Then
                toRename.Add(item.Key)
            End If
        Next

        For Each key In toRename
            Dim recipeComposite = New Windows.Storage.ApplicationDataCompositeValue()
            recipeComposite("Folder") = NewName
            recipeComposite("Recipe") = storedRecords.Values(key)("Recipe")
            storedRecords.Values.Remove(key)
            storedRecords.Values(key) = recipeComposite
        Next

        Invalidate()

    End Sub

    Public Sub ChangeCategory(ByRef recipeToChange As Recipe, ByRef NewCategory As String)

        Dim instanceInFavorites As Recipe = GetRecipe(recipeToChange.Categegory, recipeToChange.Name)
        If instanceInFavorites IsNot Nothing AndAlso Not ReferenceEquals(instanceInFavorites, recipeToChange) Then
            instanceInFavorites.Categegory = NewCategory
            instanceInFavorites.RenderSubTitle()
            instanceInFavorites.Notes = recipeToChange.Notes
        End If

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        Dim storedRecords = roamingSettings.CreateContainer("Favorites", Windows.Storage.ApplicationDataCreateDisposition.Always)

        For Each item In storedRecords.Values
            If item.Value("Folder") = recipeToChange.Categegory And item.Value("Recipe") = recipeToChange.Name Then
                Dim recipeComposite = New Windows.Storage.ApplicationDataCompositeValue()
                recipeComposite("Folder") = NewCategory
                recipeComposite("Recipe") = recipeToChange.Name
                storedRecords.Values.Remove(item.Key)
                storedRecords.Values(item.Key) = recipeComposite
                Return
            End If
        Next

    End Sub

End Class
