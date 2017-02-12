Imports Windows.Globalization.DateTimeFormatting
Imports Windows.Storage
Imports Windows.Storage.Pickers
Imports Windows.UI.Popups

Public Class History
    Inherits RecipeFolder

#Region "Properties"
    Public Property SelectionEndDate As Date = Date.Now

    Private _CategoryFilter As String = String.Empty
    Public Property CategoryFilter As String
        Set(value As String)
            If Not _CategoryFilter.Equals(value) Then
                _CategoryFilter = value
                _ContentLoaded = False
                If HistoryDB IsNot Nothing Then
                    HistoryDB.CategoryFilter = _CategoryFilter
                End If
            End If
        End Set
        Get
            Return _CategoryFilter
        End Get
    End Property

    Public ReadOnly Property IsInitialized As Boolean
        Get
            Return HistoryDB IsNot Nothing
        End Get
    End Property
#End Region

    Public Shared FolderName As String = App.Texts.GetString("HistoryFolder")
    Public Shared Current As History


    Private HistoryDB As Persistency.CookingHistory
    Private RepositoryHasBeenScanned As Boolean
    Private Dirty As Boolean


    Public Sub New()
        Me.Name = FolderName
        Current = Me
        ContentIsGrouped = True
        AddHandler App.Current.Suspending, AddressOf OnAppSuspending
        AddHandler App.Current.Resuming, AddressOf OnAppResuming
    End Sub

#Region "Lifecycle"
    Public Async Function InitAsync() As Task

        If RecipeFolders.Current IsNot Nothing AndAlso RecipeFolders.Current.GetRootFolder() IsNot Nothing Then
            HistoryDB = New Persistency.CookingHistory

            Dim rootFolder = RecipeFolders.Current.GetRootFolder()
            If Await HistoryDB.CheckDatabaseExistsAsync() Then
                ReadSettings()
            Else
                If Await HistoryDB.TryRestoreBackupAsync(rootFolder) Then
                    SetDatabaseScenned()
                Else
                    ResetScanResult()
                End If
            End If

            HistoryDB.OpenDatabase()
            HistoryDB = Persistency.CookingHistory.Current
        End If

    End Function

    Public Async Function CreateBackupAndCloseAsync() As Task
        If Dirty AndAlso RecipeFolders.Current.GetRootFolder() IsNot Nothing Then
            Await HistoryDB.CreateBackupAndCloseAsync(RecipeFolders.Current.GetRootFolder())
            Dirty = False
        ElseIf HistoryDB IsNot Nothing Then
            HistoryDB.CloseDatabase()
            HistoryDB = Nothing
        End If
    End Function

    Public Async Function TryRestoreBackupAsync() As Task

        If RecipeFolders.Current IsNot Nothing AndAlso RecipeFolders.Current.GetRootFolder() IsNot Nothing Then
            Dim rootFolder = RecipeFolders.Current.GetRootFolder()

            HistoryDB = New Persistency.CookingHistory

            If Await HistoryDB.TryRestoreBackupAsync(rootFolder) Then
                HistoryDB.OpenDatabase()
                SetDatabaseScenned()
            Else
                HistoryDB.OpenDatabase()
                ResetScanResult()
            End If

            HistoryDB = Persistency.CookingHistory.Current
        End If

    End Function

    Private Async Sub OnAppSuspending(sender As Object, e As Windows.ApplicationModel.SuspendingEventArgs)

        'Get a deferral before performing any async operations
        'to avoid suspension
        Dim Deferral = e.SuspendingOperation.GetDeferral()

        App.Logger.Write("Closing database")
        Await History.Current.CreateBackupAndCloseAsync()

        Deferral.Complete()
    End Sub

    Private Async Sub OnAppResuming(sender As Object, e As Object)

        Await InitAsync()
        App.Logger.Write("History initialized")
    End Sub

#End Region

#Region "ExportImport"

    Public Async Function ExportHistoryAsync() As Task

        If HistoryDB Is Nothing Then
            Return
        End If

        Dim picker = New FileSavePicker
        Dim historyFile As StorageFile
        Dim extensions As New List(Of String)
        Dim fileName As String

        extensions.Add(".db")
        fileName = "History_" + Date.Today.Date.ToString.Substring(0, 10) + ".db"

        picker.DefaultFileExtension = ".db"
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        picker.FileTypeChoices.Clear()
        picker.FileTypeChoices.Add("History", extensions)
        picker.SuggestedFileName = fileName

        Try
            historyFile = Await picker.PickSaveFileAsync()
        Catch ex As Exception
            Return
        End Try

        If historyFile Is Nothing Then
            Return
        End If

        Await HistoryDB.ExportHistory(historyFile)

    End Function

    Public Async Function ImportHistoryAsync() As Task

        If HistoryDB Is Nothing Then
            HistoryDB = New Persistency.CookingHistory
        End If

        Dim openPicker = New Windows.Storage.Pickers.FileOpenPicker()
        openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail

        ' Filter to include a sample subset of file types.
        openPicker.FileTypeFilter.Clear()
        openPicker.FileTypeFilter.Add(".db")

        ' Open the file picker.
        Dim historyFile As StorageFile = Await openPicker.PickSingleFileAsync()

        ' file is null if user cancels the file picker.
        If historyFile IsNot Nothing Then
            Await HistoryDB.ImportHistory(historyFile)
        End If

    End Function


#End Region

#Region "Read Access"

    Private Async Function AddSelectionToList(addMoreButton As Boolean) As Task

        Dim lastYear As Integer
        Dim lastMonth As Integer
        Dim group As RecipesGroup
        Dim startIndex As Integer = _RecipeList.Count

        If GroupedRecipes.Count > 0 Then
            group = GroupedRecipes(GroupedRecipes.Count - 1)
            GroupedRecipes.RemoveAt(GroupedRecipes.Count - 1)
            lastYear = group.Year
            lastMonth = group.Month
        End If

        For Each item In HistoryDB.Selection
            Dim newRecipe As Recipe

            newRecipe = Await RecipeFolders.Current.GetRecipeAsync(item.Category, item.Category, item.Title)
            If newRecipe Is Nothing Then
                newRecipe = New ExternalRecipe(item.Category, item.Title, Persistency.Helper.GetDate(item.LastCooked), item.ExternalSource)
            Else
                newRecipe.LastCooked = Recipe.ConvertToDateStr(Persistency.Helper.GetDate(item.LastCooked))
            End If
            Dim lastCookedDate = Recipe.ConvertToDate(newRecipe.LastCooked)
            Dim formatter As DateTimeFormatter
            If lastYear <> lastCookedDate.Year OrElse lastMonth <> lastCookedDate.Month Then
                If group IsNot Nothing Then
                    _GroupedRecipes.Add(group)
                End If
                lastYear = lastCookedDate.Year
                lastMonth = lastCookedDate.Month
                If lastYear = Date.Now.Year Then
                    formatter = New DateTimeFormatter("month.full")
                Else
                    formatter = New DateTimeFormatter("month.full year")
                End If
                group = New RecipesGroup
                group.Key = formatter.Format(lastCookedDate)
                group.Year = lastYear
                group.Month = lastMonth
            End If
            group.Add(newRecipe)
            _RecipeList.Add(newRecipe)
        Next
        If group IsNot Nothing Then ' Last group
            If addMoreButton Then
                group.Add(New RecipeListSeparator(App.Texts.GetString("MoreEntries")))
            End If
            _GroupedRecipes.Add(group)
        End If

    End Function

    Public Async Function SelectMoreRecipes(fromScratch As Boolean) As Task

        If HistoryDB Is Nothing OrElse _RecipeList.Count >= HistoryDB.Count Then
            Return
        End If

        ' Remove "More..."
        If _GroupedRecipes.Count > 0 Then
            Dim lastGroup = _GroupedRecipes(_GroupedRecipes.Count - 1)
            lastGroup.RemoveAt(lastGroup.Count - 1)
        End If

        Dim selectedUntil As Date
        Dim newStart As Date

        If fromScratch Then
            selectedUntil = Date.Now
            If SelectionEndDate = Date.Now Then
                newStart = Date.Now - New System.TimeSpan(90, 0, 0, 0)
            Else
                newStart = SelectionEndDate
            End If
        Else
            selectedUntil = SelectionEndDate
            newStart = SelectionEndDate - New System.TimeSpan(90, 0, 0, 0)
        End If

        HistoryDB.Selection.Clear()

        Dim count As Integer
        Dim matchCount As Integer = HistoryDB.Count
        Do
            count = count + HistoryDB.AddWithInterval(newStart, selectedUntil)
            If count > 30 OrElse _RecipeList.Count + count >= matchCount OrElse newStart = Date.MinValue Then
                SelectionEndDate = newStart
                Exit Do
            Else
                selectedUntil = newStart
                If newStart.Year < 2013 Then
                    newStart = Date.MinValue ' Safety net
                Else
                    newStart = newStart - New System.TimeSpan(90, 0, 0, 0)
                End If
            End If
        Loop

        Await AddSelectionToList(_RecipeList.Count + count < matchCount And newStart > Date.MinValue)
    End Function

    Public Overrides Async Function LoadAsync() As Task

        If HistoryDB Is Nothing OrElse _ContentLoaded Then
            Return
        End If

        Await ScanRepositoryCheck()

        _RecipeList.Clear()
        _GroupedRecipes.Clear()

        _ContentLoaded = True

        If HistoryDB.Count = 0 Then
            Return
        End If

        Await SelectMoreRecipes(True)
    End Function

#End Region

#Region "Repository Scan"
    Public Sub ResetScanResult()
        Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
        Dim settings = localSettings.CreateContainer("History", Windows.Storage.ApplicationDataCreateDisposition.Always)

        settings.Values("RepositoryScanned") = False
        RepositoryHasBeenScanned = False

        If HistoryDB IsNot Nothing Then
            HistoryDB.DeleteAll()
        End If
        _ContentLoaded = False
    End Sub

    Private Sub ReadSettings()
        Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
        Dim settings = localSettings.CreateContainer("History", Windows.Storage.ApplicationDataCreateDisposition.Always)

        RepositoryHasBeenScanned = settings.Values("RepositoryScanned")

        If Not RepositoryHasBeenScanned Then
            ' Initially, the setting were stored as roaming settings, which is wrong
            Dim roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings
            settings = roamingSettings.CreateContainer("History", Windows.Storage.ApplicationDataCreateDisposition.Always)

            RepositoryHasBeenScanned = settings.Values("RepositoryScanned")

            If RepositoryHasBeenScanned Then
                SetDatabaseScenned() ' Store as local settings
            End If
        End If
    End Sub

    Public Sub SetDatabaseScenned()
        Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
        Dim settings = localSettings.CreateContainer("History", Windows.Storage.ApplicationDataCreateDisposition.Always)

        settings.Values("RepositoryScanned") = True
        RepositoryHasBeenScanned = True
    End Sub

    Enum Choices
        ScanNow
        ScanLater
        DontAsk
    End Enum

    Private Choice As Choices

    Public Async Function ScanRepositoryCheck() As Task

        If HistoryDB Is Nothing OrElse RepositoryHasBeenScanned Then
            Return
        End If

        Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("DoYouWantToScan"))

        ' Add buttons and set their callbacks
        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("Yes"), Sub(command)
                                                                                 Choice = Choices.ScanNow
                                                                             End Sub))

        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("Later"), Sub(command)
                                                                                   Choice = Choices.ScanLater
                                                                               End Sub))

        'messageDialog.Commands.Add(New UICommand(App.Texts.GetString("DontAsk"), Sub(command)
        '                                                                             Choice = Choices.DontAsk
        '                                                                         End Sub))
        ' Set the command that will be invoked by default
        messageDialog.DefaultCommandIndex = 0

        ' Set the command to be invoked when escape is pressed
        messageDialog.CancelCommandIndex = 1

        Await messageDialog.ShowAsync()

        Select Case Choice
            Case Choices.ScanNow
                Await ScanRepository()
            Case Choices.ScanLater
                Return
            Case Choices.DontAsk
        End Select

        SetDatabaseScenned()

    End Function

    Public Async Function RescanRepositoryCheck() As Task

        If HistoryDB Is Nothing Then
            Return
        End If

        Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("DoYouWantToRescan"))

        ' Add buttons and set their callbacks
        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("Yes"), Sub(command)
                                                                                 Choice = Choices.ScanNow
                                                                             End Sub))

        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("No"), Sub(command)
                                                                                Choice = Choices.ScanLater
                                                                            End Sub))

        ' Set the command that will be invoked by default
        messageDialog.DefaultCommandIndex = 1

        ' Set the command to be invoked when escape is pressed
        messageDialog.CancelCommandIndex = 1

        Await messageDialog.ShowAsync()

        Select Case Choice
            Case Choices.ScanNow
                Await RescanRepository()
            Case Choices.ScanLater
                Return
        End Select

        SetDatabaseScenned()

    End Function

    Public Async Function RescanRepository() As Task
        If HistoryDB Is Nothing Then
            Return
        End If

        HistoryDB.SelectAll(onlyExternalRecipes:=True)
        HistoryDB.DeleteAll()

        For Each recipe In HistoryDB.Selection
            HistoryDB.LogRecipeCooked(recipe.Category, recipe.Title, recipe.LastCooked, recipe.ExternalSource)
        Next

        Await ScanRepository()
    End Function


    Private Async Function ScanRepository() As Task
        If HistoryDB Is Nothing Then
            Return
        End If
        For Each category In RecipeFolders.Current.Folders
            Await category.LoadAsync()

            For Each entry In category.Recipes
                If entry.CookedNoOfTimes > 0 Then
                    Try
                        AddRecipe(entry)
                    Catch ex As Exception
                    End Try
                End If
            Next
        Next
    End Function

#End Region

#Region "Change History"
    Public Sub AddRecipe(ByVal cookedRecipe As Recipe)

        If HistoryDB Is Nothing Then
            Return
        End If

        HistoryDB.LogRecipeCooked(cookedRecipe.Category, cookedRecipe.Name, Recipe.ConvertToDate(cookedRecipe.LastCooked), cookedRecipe.ExternalSource)
        Dirty = True
    End Sub



    Public Overrides Function DeleteRecipe(ByRef recipeToDelete As Recipe) As Boolean

        ' The recipe stays in the history.
        Return False
    End Function


    Public Sub RenameCategory(ByRef OldName As String, ByRef NewName As String)

        If HistoryDB Is Nothing Then
            Return
        End If

        HistoryDB.RenameCategory(OldName, NewName)
        Dirty = True
        Invalidate()
    End Sub

    Public Overrides Sub ChangeCategory(ByRef recipeToChange As Recipe, ByRef oldCategpry As String, ByRef newCategory As String)
        If HistoryDB Is Nothing Then
            Return
        End If

        MyBase.ChangeCategory(recipeToChange, oldCategpry, newCategory)

        HistoryDB.ChangeCategory(recipeToChange.Name, oldCategpry, newCategory)
        Dirty = True

    End Sub

    Public Overrides Sub RenameRecipe(ByRef recipeToRename As Recipe, ByVal oldName As String, ByVal newName As String)

        If HistoryDB Is Nothing Then
            Return
        End If

        MyBase.RenameRecipe(recipeToRename, oldName, newName)
        HistoryDB.RenameRecipe(oldName, recipeToRename.Category, newName)
        Dirty = True

    End Sub

#End Region

End Class
