Imports Windows.Storage

Public Class SearchResults
    Inherits RecipeFolder

    Public Shared FolderName As String = App.Texts.GetString("SearchResultFolder")
    Public Property LastSearchString As String

    Public Sub New()
        Name = FolderName
    End Sub

    Public Sub SetSearchParameter(SearchFolder As String, ByVal SearchString As String)

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        roamingSettings.Values("SearchString") = New String(SearchString)
        roamingSettings.Values("SearchFolder") = New String(SearchFolder)
        _ContentLoaded = False
        LastSearchString = SearchString

    End Sub

    Public Overrides Async Function LoadAsync() As Task

        If _ContentLoaded Then
            Return
        End If

        _RecipeList.Clear()
        _Recipes.Clear()

        Dim FolderDirectory = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        Dim searchString = roamingSettings.Values("SearchString")
        Dim searchFolder = roamingSettings.Values("SearchFolder")

        If searchFolder Is Nothing Or searchString Is Nothing Then
            Return
        End If

        Dim queryOptions = New Windows.Storage.Search.QueryOptions()
        queryOptions.ApplicationSearchFilter = searchString
        queryOptions.IndexerOption = Windows.Storage.Search.IndexerOption.UseIndexerWhenAvailable
        queryOptions.FolderDepth = Windows.Storage.Search.FolderDepth.Deep

        Dim startFolder = Await FolderDirectory.GetStorageFolderAsync(searchFolder)
        Dim query = startFolder.CreateFileQueryWithOptions(queryOptions)

        Dim result = Await query.GetFilesAsync()

        Await SetUpFolderFromFileListAsync(result)

    End Function

    Sub Clear()

        Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
        roamingSettings.Values("SearchString") = ""
        roamingSettings.Values("SearchFolder") = ""

    End Sub

    Public Sub ChangeCategory(ByRef recipeToChange As Recipe, ByRef NewCategory As String)

        If Not ContentLoaded() Then
            Return
        End If

        Dim instanceInSearchResults As Recipe = GetRecipe(recipeToChange.Categegory, recipeToChange.Name)
        If instanceInSearchResults Is Nothing Then
            Return
        End If

        If Not ReferenceEquals(instanceInSearchResults, recipeToChange) Then
            instanceInSearchResults.Categegory = NewCategory
            instanceInSearchResults.RenderSubTitle()
            instanceInSearchResults.Notes = recipeToChange.Notes
        End If

    End Sub

End Class
