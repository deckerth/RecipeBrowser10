' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports RecipeBrowser
Imports Windows.ApplicationModel.DataTransfer
Imports Windows.Foundation.Metadata
Imports Windows.Storage
Imports Windows.UI.Core
Imports Windows.UI.Popups
Imports Windows.UI.Xaml.Media.Animation
''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class RecipesPage
    Inherits Page

    Private CurrentRecipeFolder As RecipeFolder
    Public Property OtherCategoryList As New ObservableCollection(Of RecipeFolder)

    Public Property TimerController As Timers.Controller

    Private _lastSelectedItem As Recipe
    ''' <summary>
    ''' NavigationHelper wird auf jeder Seite zur Unterstützung bei der Navigation verwendet und 
    ''' Verwaltung der Prozesslebensdauer
    ''' </summary>
    Public ReadOnly Property NavigationHelper As Common.NavigationHelper
        Get
            Return Me._navigationHelper
        End Get
    End Property
    Private _navigationHelper As Common.NavigationHelper


    Public Sub New()
        InitializeComponent()
        Me._navigationHelper = New Common.NavigationHelper(Me)
        AddHandler Me._navigationHelper.LoadState, AddressOf NavigationHelper_LoadState
        AddHandler Me._navigationHelper.SaveState, AddressOf NavigationHelper_SaveState

        TimerController = Timers.Controller.Current

        Dim manager = DataTransferManager.GetForCurrentView()
        AddHandler manager.DataRequested, AddressOf DataRequestedManager
    End Sub


    ''' <summary>
    ''' Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird.  Gespeicherte Zustände werden ebenfalls
    ''' bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
    ''' </summary>
    ''' 
    ''' Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/>
    ''' 
    ''' <param name="e">Ereignisdaten, die die Navigationsparameter bereitstellen, die an
    ''' <see cref="Frame.Navigate"/> übergeben wurde, als diese Seite ursprünglich angefordert wurde und
    ''' ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
    ''' beibehalten wurde.  Der Zustand ist beim ersten Aufrufen einer Seite NULL.</param>
    ''' 
    Private Async Sub NavigationHelper_LoadState(sender As Object, e As Common.LoadStateEventArgs)
        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        Dim category = DirectCast(e.NavigationParameter, String)

        DisableControls(False) ' do not show action progress

        LoadProgress.Visibility = Visibility.Visible
        CurrentRecipeFolder = Await categories.GetFolderAsync(category)
        LoadProgress.Visibility = Visibility.Collapsed

        If CurrentRecipeFolder.ContentIsGrouped Then
            GroupedRecipesCVS.Source = CurrentRecipeFolder.GroupedRecipes
            MasterListView.ItemsSource = GroupedRecipesCVS.View
        Else
            MasterListView.ItemsSource = CurrentRecipeFolder.Recipes
        End If

        pageTitle.Text = category

        If category = Favorites.FolderName Then
            RemoveFromFavorites.Visibility = Visibility.Visible
            AddToFavorites.Visibility = Visibility.Collapsed
            ShowFavoritesButton.Visibility = Visibility.Collapsed
            RecipeSearchBox.Visibility = Visibility.Collapsed
            RecipeAutoSuggestBox.Visibility = Visibility.Collapsed
            changeSortOrder.Visibility = Visibility.Visible
            MasterListView.ItemTemplate = DirectCast(Resources("RecipeItemListTemplate"), DataTemplate)
        ElseIf category = History.FolderName Then
            RemoveFromFavorites.Visibility = Visibility.Collapsed
            AddToFavorites.Visibility = Visibility.Visible
            ShowFavoritesButton.Visibility = Visibility.Visible
            setFilter.Visibility = Visibility.Visible
            deleteFilter.Visibility = Visibility.Visible
            'addExternalRecipe.Visibility = Visibility.Visible
            RecipeSearchBox.Visibility = Visibility.Collapsed
            RecipeAutoSuggestBox.Visibility = Visibility.Collapsed
            changeSortOrder.Visibility = Visibility.Collapsed
            ShowHistoryButton.Visibility = Visibility.Collapsed
            MasterListView.ItemTemplate = DirectCast(Resources("HistoryItemListTemplate"), DataTemplate)
        Else
            RemoveFromFavorites.Visibility = Visibility.Collapsed
            AddToFavorites.Visibility = Visibility.Visible
            ShowFavoritesButton.Visibility = Visibility.Visible
            changeSortOrder.Visibility = Visibility.Visible
            'RecipeSearchBox.Visibility = App.SearchBoxVisibility
            RecipeAutoSuggestBox.Visibility = Visibility.Visible
            MasterListView.ItemTemplate = DirectCast(Resources("RecipeItemListTemplate"), DataTemplate)
        End If

        If category = SearchResults.FolderName Then
            If App.SearchBoxIsSupported Then
                RecipeSearchBox.QueryText = categories.SearchResultsFolder.LastSearchString
            Else
                RecipeAutoSuggestBox.Text = categories.SearchResultsFolder.LastSearchString
            End If
        End If

        EnableControls()

        If CurrentRecipeFolder.Folder IsNot Nothing Then
            If App.SearchBoxIsSupported Then
                Try
                    Dim searchSuggestions = New Windows.ApplicationModel.Search.LocalContentSuggestionSettings()
                    searchSuggestions.Enabled = True
                    searchSuggestions.Locations.Add(CurrentRecipeFolder.Folder)
                    RecipeSearchBox.SetLocalContentSuggestionSettings(searchSuggestions)
                Catch ex As Exception
                End Try
            End If
        End If

        If e.PageState Is Nothing Then
            ' Wenn es sich hierbei um eine neue Seite handelt, das erste Element automatisch auswählen, außer wenn
            ' logische Seitennavigation verwendet wird (weitere Informationen in der #Region zur logischen Seitennavigation unten).
            If CurrentRecipeFolder.Recipes.Count > 0 Then
                MasterListView.SelectedIndex = 0
            Else
                MasterListView.SelectedIndex = -1
            End If
        Else
            ' Den zuvor gespeicherten Zustand wiederherstellen, der dieser Seite zugeordnet ist
            If e.PageState.ContainsKey("SelectedItem") Then
                Dim selectedItemCategory = DirectCast(e.PageState("SelectedItemCategory"), String)
                Dim selectedItem = Await categories.GetRecipeAsync(category, selectedItemCategory, DirectCast(e.PageState("SelectedItem"), String))
                If selectedItem IsNot Nothing Then
                    MasterListView.SelectedItem = selectedItem
                End If
            End If
            If CurrentRecipeFolder.Name = History.FolderName AndAlso e.PageState.ContainsKey("HistoryStartDate") Then
                History.Current.SelectionEndDate = DirectCast(e.PageState("HistoryStartDate"), Date)
            End If
        End If

        'Await Timers.Factory.Current.WakeUp()
        Await DisplayCurrentItemDetail()

    End Sub

    ''' <summary>
    ''' Behält den dieser Seite zugeordneten Zustand bei, wenn die Anwendung angehalten oder
    ''' die Seite im Navigationscache verworfen wird.  Die Werte müssen den Serialisierungsanforderungen
    ''' von <see cref="Common.SuspensionManager.SessionState"/> entsprechen.
    ''' </summary>
    ''' <param name="sender">
    ''' Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/>
    ''' </param>
    ''' <param name="e">Ereignisdaten, die ein leeres Wörterbuch zum Auffüllen bereitstellen 
    ''' serialisierbarer Zustand.</param>
    Private Sub NavigationHelper_SaveState(sender As Object, e As Common.SaveStateEventArgs)
        ' TODO: Einen serialisierbaren Navigationsparameter ableiten und ihn
        '       pageState("SelectedItem")
        If _lastSelectedItem IsNot Nothing Then
            Dim itemTitle = _lastSelectedItem.Name
            e.PageState("SelectedItem") = itemTitle
            Dim itemCategory = _lastSelectedItem.Category
            e.PageState("SelectedItemCategory") = itemCategory
        End If

        If CurrentRecipeFolder.Name = History.FolderName Then
            e.PageState("HistoryStartDate") = History.Current.SelectionEndDate
        End If

    End Sub

#Region "NavigationHelper-Registrierung"

    ''' Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
    ''' damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
    ''' 
    ''' Platzieren Sie seitenspezifische Logik in Ereignishandlern für  
    ''' <see cref="Common.NavigationHelper.LoadState"/>
    ''' and <see cref="Common.NavigationHelper.SaveState"/>.
    ''' Der Navigationsparameter ist in der LoadState-Methode verfügbar 
    ''' zusätzlich zum Seitenzustand, der während einer früheren Sitzung beibehalten wurde.

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        Dim currentView = SystemNavigationManager.GetForCurrentView()

        If Not ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons") Then
            If Me.Frame.CanGoBack Then
                currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
            Else
                currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
            End If
        End If

        _navigationHelper.OnNavigatedTo(e)
    End Sub


    Protected Overrides Sub OnNavigatedFrom(e As NavigationEventArgs)
        _navigationHelper.OnNavigatedFrom(e)
    End Sub

#End Region

#Region "AdaptiveStates"

    Private Sub AdaptiveStates_CurrentStateChanged(sender As Object, e As VisualStateChangedEventArgs)

        UpdateForVisualState(e.NewState, e.OldState)
    End Sub

    Private Sub UpdateForVisualState(newState As VisualState, oldState As VisualState)

        Dim isNarrow As Boolean = (newState.Equals(NarrowState) Or newState.Equals(PhoneState))
        'Dim isNarrow As Boolean = newState.Equals(NarrowState)

        If (isNarrow And oldState.Equals(DefaultState) And _lastSelectedItem IsNot Nothing) Then
            ' Resize down to the detail item. Don't play a transition.
            Frame.Navigate(GetType(RecipePage), _lastSelectedItem.GetKey(CurrentRecipeFolder.Name), New SuppressNavigationTransitionInfo())
        End If

        EntranceNavigationTransitionInfo.SetIsTargetElement(MasterListView, isNarrow)
        If DetailContentPresenter IsNot Nothing Then
            EntranceNavigationTransitionInfo.SetIsTargetElement(DetailContentPresenter, Not isNarrow)
        End If
    End Sub

    Private Sub LayoutRoot_Loaded(sender As Object, e As RoutedEventArgs)
        'Assure we are displaying the correct item. This Is necessary in certain adaptive cases.
        MasterListView.SelectedItem = _lastSelectedItem
    End Sub

    Private Sub EnableContentTransitions()

        DetailContentPresenter.ContentTransitions.Clear()
        DetailContentPresenter.ContentTransitions.Add(New EntranceThemeTransition())
    End Sub

    Private Sub DisableContentTransitions()

        If DetailContentPresenter IsNot Nothing Then
            DetailContentPresenter.ContentTransitions.Clear()
        End If
    End Sub

#End Region

#Region "MasterDetailHandling"
    Private Async Function DisplayCurrentItemDetail() As Task

        _lastSelectedItem = MasterListView.SelectedItem

        If AdaptiveStates.CurrentState.Equals(DefaultState) Then
            If _lastSelectedItem IsNot Nothing Then
                If _lastSelectedItem.File IsNot Nothing Then
                    Await _lastSelectedItem.LoadRecipeAsync()
                    RecipeViewer.Source = _lastSelectedItem.RenderedPage
                Else
                    Dim folder As RecipeFolder = Await RecipeFolders.Current.GetFolderAsync(_lastSelectedItem.Category)
                    If folder IsNot Nothing Then
                        Await folder.GetImagesOfRecipeAsync(_lastSelectedItem)
                        If _lastSelectedItem.Pictures IsNot Nothing AndAlso _lastSelectedItem.Pictures.Count > 0 Then
                            RecipeViewer.Source = _lastSelectedItem.Pictures(0).Image
                        Else
                            RecipeViewer.Source = Nothing
                        End If
                    Else
                        RecipeViewer.Source = Nothing
                    End If
                End If
            Else
                RecipeViewer.Source = Nothing
            End If
        End If
        EnableControls()

    End Function

    Private Async Function SelectRecipe(selectedRecipe As Recipe) As Task
        MasterListView.SelectedItem = selectedRecipe

        Select Case selectedRecipe.ItemType
            Case Recipe.ItemTypes.Recipe, Recipe.ItemTypes.ExternalRecipe
                _lastSelectedItem = selectedRecipe

                If AdaptiveStates.CurrentState.Equals(NarrowState) Or AdaptiveStates.CurrentState.Equals(PhoneState) Then
                    ' Use "drill in" transition for navigating from master list to detail view
                    'If _lastSelectedItem.File IsNot Nothing Then
                    Frame.Navigate(GetType(RecipePage), selectedRecipe.GetKey(CurrentRecipeFolder.Name), New DrillInNavigationTransitionInfo())
                    'End If
                Else
                    ' Play a refresh animation when the user switches detail items.
                    Await DisplayCurrentItemDetail()

                    EnableContentTransitions()
                End If

            'Case Recipe.ItemTypes.ExternalRecipe
            '    _lastSelectedItem = selectedRecipe
            '    EnableControls()

            Case Recipe.ItemTypes.Header
                Dim item As Recipe
                If MasterListView.SelectedIndex > 0 Then
                    item = MasterListView.Items(MasterListView.SelectedIndex - 1)
                End If
                Await History.Current.SelectMoreRecipes(False)
                If item IsNot Nothing Then
                    MasterListView.ScrollIntoView(item, ScrollIntoViewAlignment.Leading)
                End If

        End Select

    End Function

    Private Async Sub ItemListView_SelectionChanged(sender As Object, e As ItemClickEventArgs)

        Dim clickedItem As Recipe = DirectCast(e.ClickedItem, Recipe)

        Await SelectRecipe(clickedItem)

    End Sub
#End Region

#Region "ItemSorter"
    Private Sub SetSortOrder(ByRef sortOrder As RecipeFolder.SortOrder)

        CurrentRecipeFolder.SetSortOrder(sortOrder)
        MasterListView.ItemsSource = CurrentRecipeFolder.Recipes

        If _lastSelectedItem IsNot Nothing Then
            MasterListView.SelectedItem = _lastSelectedItem
        End If
    End Sub

    Private Sub SortNameAscending_Click(sender As Object, e As RoutedEventArgs)

        SetSortOrder(RecipeFolder.SortOrder.ByNameAscending)

    End Sub

    Private Sub SortDateDecending_Click(sender As Object, e As RoutedEventArgs)

        SetSortOrder(RecipeFolder.SortOrder.ByDateDescending)

    End Sub

    Private Sub SortLastCookedDescending_Click(sender As Object, e As RoutedEventArgs)

        SetSortOrder(RecipeFolder.SortOrder.ByLastCookedDescending)

    End Sub

#End Region

#Region "PageControl"
    Private Sub RenderPageControl(ByRef CurrentRecipe As Recipe)

        If CurrentRecipe Is Nothing Then
            pageControl.Visibility = Visibility.Collapsed
        Else
            If CurrentRecipe.NoOfPages > 1 Then
                pageNumber.Text = CurrentRecipe.CurrentPage.ToString + "/" + CurrentRecipe.NoOfPages.ToString
                pageControl.Visibility = Visibility.Visible
                If CurrentRecipe.CurrentPage = 1 Then
                    prevPage.IsEnabled = False
                Else
                    prevPage.IsEnabled = True

                End If
                If CurrentRecipe.CurrentPage = CurrentRecipe.NoOfPages Then
                    nextPage.IsEnabled = False
                Else
                    nextPage.IsEnabled = True
                End If
            Else
                pageControl.Visibility = Visibility.Collapsed
            End If
        End If

    End Sub

    Private Async Sub GotoPreviousPage(sender As Object, e As RoutedEventArgs) Handles prevPage.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If
        DisableControls()
        Await _lastSelectedItem.PreviousPage()
        RecipeViewer.Source = _lastSelectedItem.RenderedPage
        EnableControls()

    End Sub

    Private Async Sub GotoNextPage(sender As Object, e As RoutedEventArgs) Handles nextPage.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If
        DisableControls()
        Await _lastSelectedItem.NextPage()
        RecipeViewer.Source = _lastSelectedItem.RenderedPage
        EnableControls()
    End Sub

#End Region

#Region "EnableDisableControls"
    Private Sub DisableControls(Optional visualizeProgress As Boolean = True)

        ShowFavorites.IsEnabled = False
        Home.IsEnabled = False
        ShowHistory.IsEnabled = False
        refreshRecipes.IsEnabled = False
        FolderSelection.IsEnabled = False
        ShowTimers.IsEnabled = False
        If visualizeProgress Then
            actionProgress.IsActive = True
        End If
        RecipeSearchBox.IsEnabled = False
        RecipeAutoSuggestBox.IsEnabled = False
        nextPage.IsEnabled = False
        prevPage.IsEnabled = False
        refreshRecipes.IsEnabled = False
        AddToFavorites.IsEnabled = False
        RemoveFromFavorites.IsEnabled = False
        OpenFile.IsEnabled = False
        ChangeCategory.IsEnabled = False
        Menu.IsEnabled = False
        deleteRecipe.IsEnabled = False
        RenameRecipe.IsEnabled = False
        editNote.IsEnabled = False
        logAsCooked.IsEnabled = False
        FullscreenView.IsEnabled = False
        Share.IsEnabled = False
        setFilter.IsEnabled = False
        deleteFilter.IsEnabled = False
        'addExternalRecipe.IsEnabled = False
        ShowImageGalery.IsEnabled = False
        AddRecipe.IsEnabled = False
        ExportImport.IsEnabled = False
    End Sub

    Private Sub EnableControls()

        ShowFavorites.IsEnabled = True
        Home.IsEnabled = True
        actionProgress.IsActive = False
        refreshRecipes.IsEnabled = True
        Menu.IsEnabled = True
        ShowHistory.IsEnabled = True
        FolderSelection.IsEnabled = True
        ShowTimers.IsEnabled = True
        AddRecipe.IsEnabled = True
        ExportImport.IsEnabled = True

        If CurrentRecipeFolder.Name <> SearchResults.FolderName Then
            RecipeSearchBox.IsEnabled = True
            RecipeAutoSuggestBox.IsEnabled = True
        End If

        If CurrentRecipeFolder.Name <> SearchResults.FolderName And
           CurrentRecipeFolder.Name <> Favorites.FolderName Then
            AddRecipeButton.Visibility = Visibility.Visible
        Else
            AddRecipeButton.Visibility = Visibility.Collapsed
        End If

        If CurrentRecipeFolder.Name = History.FolderName Then
            ExportImportButton.Visibility = Visibility.Visible
        Else
            ExportImportButton.Visibility = Visibility.Collapsed
        End If

        If History.Current.IsInitialized Then
            ExportHistoryMenuItem.IsEnabled = True
        Else
            ExportHistoryMenuItem.IsEnabled = False
        End If

        RenderPageControl(_lastSelectedItem) ' currentRecipe may be nothing

        If _lastSelectedItem Is Nothing Then
            AddToFavorites.IsEnabled = False
            RemoveFromFavorites.IsEnabled = False
            OpenFile.IsEnabled = False
            ChangeCategory.IsEnabled = False
            deleteRecipe.IsEnabled = False
            RenameRecipe.IsEnabled = False
            logAsCooked.IsEnabled = False
            FullscreenView.IsEnabled = False
            editNote.Label = ""
            editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Gray))
            editNote.IsEnabled = False
            Share.IsEnabled = False
            ShowImageGalery.IsEnabled = False
        Else
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

            If _lastSelectedItem.File IsNot Nothing Then
                OpenFile.IsEnabled = True
                Share.IsEnabled = True
                If _lastSelectedItem.RenderedPage IsNot Nothing Then
                    FullscreenView.IsEnabled = True
                Else
                    FullscreenView.IsEnabled = False
                End If
                editNote.IsEnabled = True
                If _lastSelectedItem.Notes Is Nothing Then
                    editNote.Label = App.Texts.GetString("CreateNote")
                    editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Black))
                Else
                    editNote.Label = App.Texts.GetString("DisplayNote")
                    editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Orange))
                End If
            Else
                editNote.IsEnabled = False
                FullscreenView.IsEnabled = False
                OpenFile.IsEnabled = False
                Share.IsEnabled = False
            End If

            ShowImageGalery.IsEnabled = True
            AddToFavorites.IsEnabled = True
            RemoveFromFavorites.IsEnabled = True
            If CurrentRecipeFolder.Name = Favorites.FolderName OrElse categories.FavoriteFolder.IsFavorite(_lastSelectedItem) Then
                RemoveFromFavorites.Visibility = Visibility.Visible
                AddToFavorites.Visibility = Visibility.Collapsed
            Else
                RemoveFromFavorites.Visibility = Visibility.Collapsed
                AddToFavorites.Visibility = Visibility.Visible
            End If

            If CurrentRecipeFolder.Name = History.FolderName Then
                deleteRecipe.IsEnabled = False
                logAsCooked.IsEnabled = False
            Else
                deleteRecipe.IsEnabled = _lastSelectedItem IsNot Nothing AndAlso _lastSelectedItem.ItemType = Recipe.ItemTypes.Recipe
                logAsCooked.IsEnabled = Not _lastSelectedItem.CookedToday()
            End If

            RenameRecipe.IsEnabled = _lastSelectedItem IsNot Nothing
            ChangeCategory.IsEnabled = _lastSelectedItem IsNot Nothing
        End If

        If CurrentRecipeFolder.Name = History.FolderName Then
            setFilter.IsEnabled = True
            deleteFilter.IsEnabled = History.Current.CategoryFilter <> String.Empty
            'addExternalRecipe.IsEnabled = True
        End If
    End Sub

#End Region

#Region "SearchBox"
    Private Sub SearchBox_QuerySubmitted(sender As SearchBox, args As SearchBoxQuerySubmittedEventArgs)

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        categories.SearchResultsFolder.SetSearchParameter(CurrentRecipeFolder.Name, args.QueryText)
        Me.Frame.Navigate(GetType(RecipesPage), SearchResults.FolderName)

    End Sub

    Private Async Sub RecipeAutoSuggestBox_QuerySubmitted(sender As AutoSuggestBox, args As AutoSuggestBoxQuerySubmittedEventArgs) Handles RecipeAutoSuggestBox.QuerySubmitted

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        If args.ChosenSuggestion Is Nothing Then
            categories.SearchResultsFolder.SetSearchParameter(CurrentRecipeFolder.Name, args.QueryText)
            Me.Frame.Navigate(GetType(RecipesPage), SearchResults.FolderName)
        Else
            Dim chosen As Recipe = args.ChosenSuggestion
            Await SelectRecipe(chosen)
            MasterListView.ScrollIntoView(chosen)
        End If

    End Sub

    Private Sub RecipeAutoSuggestBox_TextChanged(sender As AutoSuggestBox, args As AutoSuggestBoxTextChangedEventArgs) Handles RecipeAutoSuggestBox.TextChanged

        'We only want to get results when it was a user typing, 
        'otherwise we assume the value got filled in by TextMemberPath 
        'Or the handler for SuggestionChosen
        If args.Reason = AutoSuggestionBoxTextChangeReason.UserInput Then
            Dim matchincRecipes = CurrentRecipeFolder.GetMatchingRecipes(sender.Text)
            sender.ItemsSource = matchincRecipes.ToList()
        End If
    End Sub

#End Region

#Region "NoteEditor"
    Private noteTextChanged As Boolean
    Private recipeWithNote As Recipe

    Private Sub noteEditor_TextChanged(sender As Object, e As RoutedEventArgs) Handles noteEditor.TextChanged
        noteTextChanged = True
    End Sub

    Private Async Sub OpenNoteEditor_Click(sender As Object, e As RoutedEventArgs) Handles editNote.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        recipeWithNote = Nothing
        DisableControls(False) ' no progress display
        recipeWithNote = _lastSelectedItem

        If recipeWithNote.Notes IsNot Nothing Then
            Try
                Dim randAccStream As Windows.Storage.Streams.IRandomAccessStream = Await recipeWithNote.Notes.OpenAsync(Windows.Storage.FileAccessMode.Read)
                noteEditor.Document.LoadFromStream(Windows.UI.Text.TextSetOptions.FormatRtf, randAccStream)
            Catch ex As Exception
            End Try
        End If

        noteTextChanged = False
    End Sub

    Private Async Sub NoteEditorFlyoutClosed(sender As Object, e As Object)

        actionProgress.IsActive = True

        If noteTextChanged Then
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
            Await recipeWithNote.UpdateNoteTextAsync(noteEditor.Document)
        End If

        EnableControls()

    End Sub


#End Region

#Region "ContentSharing"
    Private Sub Share_Click(sender As Object, e As RoutedEventArgs) Handles Share.Click

        Windows.ApplicationModel.DataTransfer.DataTransferManager.ShowShareUI()

    End Sub

    Private Sub DataRequestedManager(sender As DataTransferManager, args As DataRequestedEventArgs)

        ' Share a recipe

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        Dim request = args.Request
        Dim storageItems As New List(Of IStorageItem)

        storageItems.Add(_lastSelectedItem.File)

        request.Data.Properties.Title = App.Texts.GetString("Recipe")
        request.Data.Properties.Description = _lastSelectedItem.Name
        request.Data.SetStorageItems(storageItems)

    End Sub
#End Region

#Region "OpenAndFullscreen"
    Private Async Sub OpenRecipe_Click(sender As Object, e As RoutedEventArgs) Handles OpenFile.Click

        If _lastSelectedItem IsNot Nothing Then
            DisableControls()
            Await Windows.System.Launcher.LaunchFileAsync(_lastSelectedItem.File)
            EnableControls()
        End If

    End Sub

    Private Sub FullscreenView_Click(sender As Object, e As RoutedEventArgs) Handles FullscreenView.Click

        If _lastSelectedItem IsNot Nothing Then
            Frame.Navigate(GetType(RecipePage), _lastSelectedItem.GetKey(CurrentRecipeFolder.Name))
        End If

    End Sub
#End Region

#Region "Favorites"
    Private Async Sub AddToFavorites_Click(sender As Object, e As RoutedEventArgs) Handles AddToFavorites.Click

        If _lastSelectedItem IsNot Nothing Then
            DisableControls()

            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
            Await categories.FavoriteFolder.AddRecipeAsync(_lastSelectedItem)
            EnableControls()
        End If

    End Sub

    Private Async Sub RemoveFromFavorites_Click(sender As Object, e As RoutedEventArgs) Handles RemoveFromFavorites.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        Dim categories As RecipeFolders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
        If Not categories.FavoriteFolder.ContentLoaded Then
            DisableControls(True)
            Await categories.FavoriteFolder.LoadAsync()
        End If
        categories.FavoriteFolder.DeleteRecipe(_lastSelectedItem)
        If CurrentRecipeFolder.Name = Favorites.FolderName Then
            RecipeViewer.Source = Nothing
            _lastSelectedItem = Nothing
        End If
        EnableControls()

    End Sub
#End Region

#Region "Navigation"
    Private Sub ShowFavorites_Click(sender As Object, e As RoutedEventArgs) Handles ShowFavorites.Click
        RootSplitView.IsPaneOpen = False
        Me.Frame.Navigate(GetType(RecipesPage), Favorites.FolderName)
    End Sub

    Private Sub ToggleSplitView_Click(sender As Object, e As RoutedEventArgs) Handles ToggleSplitView.Click
        RootSplitView.IsPaneOpen = Not RootSplitView.IsPaneOpen
    End Sub

    Private Sub Home_Click(sender As Object, e As RoutedEventArgs) Handles Home.Click
        RootSplitView.IsPaneOpen = False
        Me.Frame.Navigate(GetType(CategoryOverview))
    End Sub

#End Region

#Region "LogAsCooked"
    Private Sub LogAsCooked_Click(sender As Object, e As RoutedEventArgs) Handles logAsCooked.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        DisableControls(False)
        CookedOn.Date = Date.Now

    End Sub


    Private Sub CookedOn_Closed(sender As Object, e As Object) Handles CookedOn.Closed
        EnableControls()
    End Sub

    Private Async Sub CookedOn_DatePicked(sender As DatePickerFlyout, args As DatePickedEventArgs) Handles CookedOn.DatePicked
        actionProgress.IsActive = True

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        Await _lastSelectedItem.LogRecipeCookedAsync(CookedOn.Date)

        EnableControls()
    End Sub

#End Region

#Region "DeleteRecipe"
    Private Cancelled As Boolean

    Private Async Sub DeleteRecipe_Click(sender As Object, e As RoutedEventArgs) Handles deleteRecipe.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("DoYouWantToDelete"))

        ' Add buttons and set their callbacks
        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("Yes"), Sub(command)
                                                                                 Cancelled = False
                                                                             End Sub))

        messageDialog.Commands.Add(New UICommand(App.Texts.GetString("No"), Sub(command)
                                                                                Cancelled = True
                                                                            End Sub))

        ' Set the command that will be invoked by default
        messageDialog.DefaultCommandIndex = 1

        ' Set the command to be invoked when escape is pressed
        messageDialog.CancelCommandIndex = 1

        Await messageDialog.ShowAsync()
        If Cancelled Then
            Return
        End If

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        RecipeViewer.Source = Nothing
        DisableControls()
        Await categories.DeleteRecipeAsync(_lastSelectedItem)
        _lastSelectedItem = Nothing
        EnableControls()

    End Sub

#End Region

#Region "RenameRecipe"
    Private Async Sub RenameRecipe_Click(sender As Object, e As RoutedEventArgs) Handles RenameRecipe.Click

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        Dim oldName As String = _lastSelectedItem.Name

        DisableControls(False)

        Dim nameEditor = New RecipeNameEditor
        nameEditor.SetAction(RecipeNameEditor.FileActions.Rename)
        nameEditor.SetCategory(CurrentRecipeFolder)
        nameEditor.SetFile(_lastSelectedItem.File)
        nameEditor.SetTitle(oldName)
        Await nameEditor.ShowAsync()

        If Not nameEditor.DialogCancelled() Then
            Dim newName As String = nameEditor.GetRecipeTitle()
            If Not oldName.Equals(newName) Then
                Await RecipeFolders.Current.RenameRecipeAsync(_lastSelectedItem, newName)
            End If
        End If

        EnableControls()
    End Sub
#End Region

#Region "RefreshRecipes"
    Private Async Function DoRefreshRecipes() As Task

        LoadProgress.Visibility = Visibility.Visible
        CurrentRecipeFolder.Invalidate()
        Await CurrentRecipeFolder.LoadAsync()
        LoadProgress.Visibility = Visibility.Collapsed

    End Function

    Private Async Sub RefreshRecipes_Click(sender As Object, e As RoutedEventArgs) Handles refreshRecipes.Click

        DisableControls(False)
        If CurrentRecipeFolder.Name = History.FolderName Then
            Await History.Current.RescanRepositoryCheck()
        End If
        Await DoRefreshRecipes()
        EnableControls()

    End Sub
#End Region

#Region "Category Chooser"
    Private Enum ChooserModes
        ForChangeCategory
        ForFolderSelection
        ForCategoryFilter
    End Enum

    Private ChooserMode As ChooserModes

    Public Sub ShowCategoryChooser()
        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        DisableControls(False)

        OtherCategoryList.Clear()
        For Each folder In categories.Folders
            If folder.Name <> CurrentRecipeFolder.Name Then
                OtherCategoryList.Add(folder)
            End If
        Next
        FolderSelectionList.ItemsSource = OtherCategoryList

        Select Case ChooserMode
            Case ChooserModes.ForFolderSelection
                CategoryChooserTitle.Visibility = Visibility.Collapsed
            Case ChooserModes.ForChangeCategory
                CategoryChooserTitle.Visibility = Visibility.Visible
                CategoryChooserTitle.Text = App.Texts.GetString("MoveTo.Text")
            Case ChooserModes.ForCategoryFilter
                CategoryChooserTitle.Visibility = Visibility.Visible
                CategoryChooserTitle.Text = App.Texts.GetString("CategoryFilter")
        End Select


        RootSplitView.IsPaneOpen = False
        FolderSelectionSplitView.IsPaneOpen = True
    End Sub

    Private Async Sub Category_Chosen(sender As Object, e As ItemClickEventArgs)
        EnableControls()

        Dim selectedItem = DirectCast(e.ClickedItem, RecipeFolder)

        If selectedItem IsNot Nothing Then
            FolderSelectionSplitView.IsPaneOpen = False
            Select Case ChooserMode
                Case ChooserModes.ForFolderSelection
                    FolderSelectionChosen(selectedItem)
                Case ChooserModes.ForChangeCategory
                    Await ChangeCategoryOfCurrentItem(selectedItem)
                Case ChooserModes.ForCategoryFilter
                    SetCategoryFilter(selectedItem)
            End Select
        End If
    End Sub

    Private Sub FolderSelectionSplitView_PaneClosed(sender As SplitView, args As Object) Handles FolderSelectionSplitView.PaneClosed
        EnableControls()
    End Sub

#End Region

#Region "ChangeCategory"
    Private Sub changeCategory_Click(sender As Object, e As RoutedEventArgs) Handles ChangeCategory.Click
        ChooserMode = ChooserModes.ForChangeCategory
        ShowCategoryChooser()
    End Sub


    Private Async Function ChangeCategoryOfCurrentItem(newCategory As RecipeFolder) As Task

        If _lastSelectedItem Is Nothing Then
            Return
        End If

        actionProgress.IsActive = True

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
        Await categories.ChangeCategoryAsync(_lastSelectedItem, newCategory)

        Await DisplayCurrentItemDetail()

        EnableControls()

    End Function
#End Region

#Region "FolderSelection"
    Private Sub FolderSelection_Click(sender As Object, e As RoutedEventArgs) Handles FolderSelection.Click

        ChooserMode = ChooserModes.ForFolderSelection

        ShowCategoryChooser()
    End Sub

    Private Sub FolderSelectionChosen(chosenCategory As RecipeFolder)
        Me.Frame.Navigate(GetType(RecipesPage), chosenCategory.Name)
    End Sub
#End Region

#Region "Timers"
    Private Sub ShowTimers_Click(sender As Object, e As RoutedEventArgs) Handles ShowTimers.Click
        TimerController.TimersPaneOpen = Not TimerController.TimersPaneOpen
    End Sub
#End Region

#Region "History"
    Private Sub ShowHistory_Click(sender As Object, e As RoutedEventArgs) Handles ShowHistory.Click
        Me.Frame.Navigate(GetType(RecipesPage), History.FolderName)
    End Sub

    Private Sub setFilter_Click(sender As Object, e As RoutedEventArgs) Handles setFilter.Click
        ChooserMode = ChooserModes.ForCategoryFilter
        ShowCategoryChooser()
    End Sub

    Private Async Sub SetCategoryFilter(selectedItem As RecipeFolder)
        DisableControls()
        History.Current.CategoryFilter = selectedItem.Name
        Await History.Current.LoadAsync()
        EnableControls()
    End Sub

    Private Async Sub deleteFilter_Click(sender As Object, e As RoutedEventArgs) Handles deleteFilter.Click
        DisableControls()
        History.Current.CategoryFilter = String.Empty
        History.Current.SelectionEndDate = Date.Now
        Await History.Current.LoadAsync()
        EnableControls()
    End Sub

    Private Async Sub addExternalRecipe_Click()
        Dim dialog As New RecipeFromExternalSource()
        Await dialog.ShowAsync()
        DisableControls()
        Await History.Current.LoadAsync()
        EnableControls()
    End Sub


#End Region

#Region "Galery"
    Private Sub ShowImageGalery_Click(sender As Object, e As RoutedEventArgs) Handles ShowImageGalery.Click
        If _lastSelectedItem IsNot Nothing Then
            Me.Frame.Navigate(GetType(RecipeImageGalery), _lastSelectedItem.GetKey(CurrentRecipeFolder.Name))
        End If
    End Sub
#End Region

#Region "AddRecipe"
    Private Async Sub AddRecipe_Click(sender As Object, e As RoutedEventArgs) Handles AddRecipe.Click

        If CurrentRecipeFolder.Name = History.FolderName Then
            addExternalRecipe_Click()
        Else
            DisableControls(False)

            Dim openPicker = New Windows.Storage.Pickers.FileOpenPicker()
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail

            ' Filter to include a sample subset of file types.
            openPicker.FileTypeFilter.Clear()
            openPicker.FileTypeFilter.Add(".pdf")

            ' Open the file picker.
            Dim file As StorageFile = Await openPicker.PickSingleFileAsync()

            ' file is null if user cancels the file picker.
            If file IsNot Nothing AndAlso file.Name.Length > 4 Then
                Try
                    Dim nameEditor = New RecipeNameEditor()
                    nameEditor.SetCategory(CurrentRecipeFolder)
                    nameEditor.SetFile(file)
                    nameEditor.SetTitle(file.Name.Remove(file.Name.Length - 4))
                    nameEditor.SetAction(RecipeNameEditor.FileActions.Copy)

                    Await nameEditor.ShowAsync()

                    If Not nameEditor.DialogCancelled() Then
                        Await DoRefreshRecipes()
                    End If

                Catch ex As Exception
                End Try
            End If

            EnableControls()
        End If

    End Sub
#End Region

#Region "ExportImportHistory"


    Private Async Sub ExportHistory_Click(sender As Object, e As RoutedEventArgs)

        DisableControls(False)

        Await History.Current.ExportHistoryAsync()

        EnableControls()

    End Sub

    Private Async Sub ImportHistory_Click(sender As Object, e As RoutedEventArgs)

        DisableControls(False)

        Await History.Current.ImportHistoryAsync()

        Await DoRefreshRecipes()

        EnableControls()

    End Sub

#End Region


End Class
