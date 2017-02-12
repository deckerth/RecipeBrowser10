' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports Windows.ApplicationModel.DataTransfer
Imports Windows.Foundation.Metadata
Imports Windows.Storage
Imports Windows.UI.Core
Imports Windows.UI.Popups
Imports Windows.UI.Xaml.Media.Animation
''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class RecipePage
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

        Dim key As String
        Dim folder As String
        Dim category As String
        Dim name As String

        key = DirectCast(e.NavigationParameter, String)
        Recipe.GetCategoryAndNameFromKey(key, folder, category, name)

        DisableControls()

        CurrentRecipeFolder = Await categories.GetFolderAsync(folder)

        pageTitle.Text = name

        If category = Favorites.FolderName Then
            RemoveFromFavorites.Visibility = Windows.UI.Xaml.Visibility.Visible
            AddToFavorites.Visibility = Windows.UI.Xaml.Visibility.Collapsed
            ShowFavoritesButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed
        Else
            RemoveFromFavorites.Visibility = Windows.UI.Xaml.Visibility.Collapsed
            AddToFavorites.Visibility = Windows.UI.Xaml.Visibility.Visible
            ShowFavoritesButton.Visibility = Windows.UI.Xaml.Visibility.Visible
        End If

        EnableControls()

        _lastSelectedItem = Await categories.GetRecipeAsync(folder, category, name)

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
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
        Else
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
        End If

        _navigationHelper.OnNavigatedTo(e)
    End Sub


    Protected Overrides Sub OnNavigatedFrom(e As NavigationEventArgs)
        _navigationHelper.OnNavigatedFrom(e)
    End Sub

#End Region

#Region "MasterDetailHandling"
    Private Async Function DisplayCurrentItemDetail() As Task

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
        EnableControls()

    End Function
#End Region

#Region "PageControl"
    Private Sub RenderPageControl(ByRef CurrentRecipe As Recipe)

        If CurrentRecipe Is Nothing Then
            pageControl.Visibility = Windows.UI.Xaml.Visibility.Collapsed
        Else
            If CurrentRecipe.NoOfPages > 1 Then
                pageNumber.Text = CurrentRecipe.CurrentPage.ToString + "/" + CurrentRecipe.NoOfPages.ToString
                pageControl.Visibility = Windows.UI.Xaml.Visibility.Visible
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
                pageControl.Visibility = Windows.UI.Xaml.Visibility.Collapsed
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
        FolderSelection.IsEnabled = False
        If visualizeProgress Then
            actionProgress.IsActive = True
        End If
        nextPage.IsEnabled = False
        prevPage.IsEnabled = False
        AddToFavorites.IsEnabled = False
        RemoveFromFavorites.IsEnabled = False
        OpenFile.IsEnabled = False
        ChangeCategory.IsEnabled = False
        Menu.IsEnabled = False
        deleteRecipe.IsEnabled = False
        RenameRecipe.IsEnabled = False
        editNote.IsEnabled = False
        logAsCooked.IsEnabled = False
        Share.IsEnabled = False

    End Sub

    Private Sub EnableControls()

        ShowFavorites.IsEnabled = True
        Home.IsEnabled = True
        actionProgress.IsActive = False
        ShowHistory.IsEnabled = True
        FolderSelection.IsEnabled = True

        RenderPageControl(_lastSelectedItem) ' currentRecipe may be nothing

        If _lastSelectedItem Is Nothing Then
            AddToFavorites.IsEnabled = False
            RemoveFromFavorites.IsEnabled = False
            OpenFile.IsEnabled = False
            changeCategory.IsEnabled = False
            Menu.IsEnabled = False
            deleteRecipe.IsEnabled = False
            logAsCooked.IsEnabled = False
            editNote.Label = ""
            editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Gray))
            editNote.IsEnabled = False
            Share.IsEnabled = False
            RenameRecipe.IsEnabled = False
        Else
            Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

            AddToFavorites.IsEnabled = True
            RemoveFromFavorites.IsEnabled = True
            If CurrentRecipeFolder.Name = Favorites.FolderName OrElse categories.FavoriteFolder.IsFavorite(_lastSelectedItem) Then
                RemoveFromFavorites.Visibility = Windows.UI.Xaml.Visibility.Visible
                AddToFavorites.Visibility = Windows.UI.Xaml.Visibility.Collapsed
            Else
                RemoveFromFavorites.Visibility = Windows.UI.Xaml.Visibility.Collapsed
                AddToFavorites.Visibility = Windows.UI.Xaml.Visibility.Visible
            End If
            OpenFile.IsEnabled = True
            changeCategory.IsEnabled = True
            RenameRecipe.IsEnabled = True
            Menu.IsEnabled = True
            deleteRecipe.IsEnabled = True
            editNote.IsEnabled = True
            logAsCooked.IsEnabled = Not _lastSelectedItem.CookedToday()
            If _lastSelectedItem.Notes Is Nothing Then
                editNote.Label = App.Texts.GetString("CreateNote")
                editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Black))
            Else
                editNote.Label = App.Texts.GetString("DisplayNote")
                editNote.SetValue(ForegroundProperty, New SolidColorBrush(Windows.UI.Colors.Orange))
            End If
            Share.IsEnabled = True
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

    Private Sub Share_Click_1(sender As Object, e As RoutedEventArgs) Handles Share.Click

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

    Private Async Function OpenFile_ClickAsync(sender As Object, e As RoutedEventArgs) As Task Handles OpenFile.Click

        If _lastSelectedItem IsNot Nothing Then
            DisableControls()
            Await Windows.System.Launcher.LaunchFileAsync(_lastSelectedItem.File)
            EnableControls()
        End If

    End Function

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

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
        If Not categories.FavoriteFolder.ContentLoaded Then
            DisableControls(True)
            Await categories.FavoriteFolder.LoadAsync()
        End If
        categories.FavoriteFolder.DeleteRecipe(_lastSelectedItem)
        If CurrentRecipeFolder.Name = Favorites.FolderName Then
            RecipeViewer.Source = Nothing
            _lastSelectedItem = Nothing
            Me.Frame.GoBack()
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

        NavigationHelper.GoBack()
    End Sub
#End Region

#Region "Category Chooser"
    Private Enum ChooserModes
        ForChangeCategory
        ForFolderSelection
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

        If ChooserMode = ChooserModes.ForFolderSelection Then
            CategoryChooserTitle.Visibility = Visibility.Collapsed
        Else
            CategoryChooserTitle.Visibility = Visibility.Visible
        End If

        RootSplitView.IsPaneOpen = False
        FolderSelectionSplitView.IsPaneOpen = True
    End Sub

    Private Async Sub Category_Chosen(sender As Object, e As ItemClickEventArgs)
        EnableControls()

        Dim selectedItem = DirectCast(e.ClickedItem, RecipeFolder)

        If selectedItem IsNot Nothing Then
            FolderSelectionSplitView.IsPaneOpen = False
            If ChooserMode = ChooserModes.ForFolderSelection Then
                FolderSelectionChosen(selectedItem)
            Else
                Await ChangeCategoryOfCurrentItem(selectedItem)
            End If
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
#End Region

#Region "Galery"
    Private Sub ShowImageGalery_Click(sender As Object, e As RoutedEventArgs) Handles ShowImageGalery.Click
        If _lastSelectedItem IsNot Nothing Then
            Me.Frame.Navigate(GetType(RecipeImageGalery), _lastSelectedItem.GetKey(CurrentRecipeFolder.Name))
        End If
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
            Dim newName = nameEditor.GetRecipeTitle()
            If Not oldName.Equals(newName) Then
                Await RecipeFolders.Current.RenameRecipeAsync(_lastSelectedItem, newName)
                pageTitle.Text = newName
            End If
        End If

        EnableControls()
    End Sub

#End Region

End Class
