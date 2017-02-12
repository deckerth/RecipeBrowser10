' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports Windows.Foundation.Metadata
Imports Windows.UI.Core
''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class CategoryOverview
    Inherits Page

    Dim Categories As RecipeFolders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

    Public Property TimerController As Timers.Controller

    Public Shared Current As CategoryOverview
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

        TimerController = Timers.Controller.Current
        Current = Me
    End Sub

    ''' <summary>
    ''' Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird.  Gespeicherte Zustände werden ebenfalls
    ''' bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
    ''' </summary>
    ''' <param name="sender">
    ''' Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/>
    ''' </param>
    ''' <param name="e">Ereignisdaten, die die Navigationsparameter bereitstellen, die an
    ''' <see cref="Frame.Navigate"/> übergeben wurde, als diese Seite ursprünglich angefordert wurde und
    ''' ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
    ''' beibehalten wurde.  Der Zustand ist beim ersten Aufrufen einer Seite NULL.</param>
    Private Async Sub NavigationHelper_LoadState(sender As Object, e As Common.LoadStateEventArgs)
        ' TODO: Me.DefaultViewModel("Items") eine bindbare Auflistung von Elementen zuweisen
        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        itemGridView.ItemsSource = categories.Folders

        RenderSearchElements(VisualStateGroup.CurrentState)

        If App.SearchBoxIsSupported Then
            Try
                Dim searchSuggestions = New Windows.ApplicationModel.Search.LocalContentSuggestionSettings()
                searchSuggestions.Enabled = True
                Dim rootFolder = Await categories.GetStorageFolderAsync("")
                searchSuggestions.Locations.Add(rootFolder)
                RecipeSearchBox.SetLocalContentSuggestionSettings(searchSuggestions)
            Catch ex As Exception
            End Try

        End If

        itemGridView.SelectedItem = Nothing

    End Sub

    Private Sub ToggleSplitView_Click(sender As Object, e As RoutedEventArgs) Handles ToggleSplitView.Click
        RootSplitView.IsPaneOpen = Not RootSplitView.IsPaneOpen
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
        Dim rootFrame As Frame = Window.Current.Content

        rootFrame.BackStack.Clear()
        Common.SuspensionManager.ResetSessionState()
        currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed

        _navigationHelper.OnNavigatedTo(e)
    End Sub


    Protected Overrides Sub OnNavigatedFrom(e As NavigationEventArgs)
        _navigationHelper.OnNavigatedFrom(e)
    End Sub

#End Region

#Region "VisualStates"
    Private Sub RenderSearchElements(state As VisualState)
        If state.Equals(Phones) Then
            LeftRecipeAutoSuggestBox.Visibility = Visibility.Collapsed
            RecipeAutoSuggestBox.Visibility = Visibility.Collapsed
            LeftRecipeSearchBox.Visibility = Visibility.Collapsed
            RecipeSearchBox.Visibility = Visibility.Collapsed
            SearchRequestButton.Visibility = Visibility.Visible
            pageTitle.Visibility = Visibility.Collapsed
        Else
            LeftRecipeAutoSuggestBox.Visibility = Visibility.Collapsed
            RecipeAutoSuggestBox.Visibility = App.AutoSuggestBoxVisibility
            LeftRecipeSearchBox.Visibility = Visibility.Collapsed
            RecipeSearchBox.Visibility = App.SearchBoxVisibility
            SearchRequestButton.Visibility = Visibility.Collapsed
            pageTitle.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub VisualStateGroup_CurrentStateChanged(sender As Object, e As VisualStateChangedEventArgs) Handles VisualStateGroup.CurrentStateChanged
        RenderSearchElements(e.NewState)
    End Sub
#End Region

#Region "Search"
    Private Sub SearchBox_QuerySubmitted(sender As SearchBox, args As SearchBoxQuerySubmittedEventArgs)


        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        categories.SearchResultsFolder.SetSearchParameter("", args.QueryText)
        RootSplitView.IsPaneOpen = False
        Me.Frame.Navigate(GetType(RecipesPage), SearchResults.FolderName)

    End Sub

    Private Sub RecipeAutoSuggestBox_QuerySubmitted(sender As AutoSuggestBox, args As AutoSuggestBoxQuerySubmittedEventArgs) Handles RecipeAutoSuggestBox.QuerySubmitted

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        categories.SearchResultsFolder.SetSearchParameter("", args.QueryText)
        RootSplitView.IsPaneOpen = False
        Me.Frame.Navigate(GetType(RecipesPage), SearchResults.FolderName)

    End Sub

    Private Sub SearchRequestButton_Click(sender As Object, e As RoutedEventArgs) Handles SearchRequestButton.Click

        SearchRequestButton.Visibility = Visibility.Collapsed
        pageTitle.Visibility = Visibility.Collapsed
        LeftRecipeSearchBox.Visibility = App.SearchBoxVisibility
        LeftRecipeAutoSuggestBox.Visibility = App.AutoSuggestBoxVisibility

    End Sub

#End Region

#Region "ChangeRootFolder"

    Private Async Function ChangeRootFolder() As Task

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        Await categories.ChangeRootFolder()

        NavigationHelper_LoadState(Nothing, Nothing)

    End Function

#End Region

#Region "Edit Categories"
    Private Sub EditMode_Checked(sender As Object, e As RoutedEventArgs) Handles EditMode.Checked
        If EditMode.IsChecked Then
            itemGridView.IsItemClickEnabled = False
            itemGridView.SelectionMode = ListViewSelectionMode.Single
            EditCategoryButton.Visibility = Visibility.Visible
            NewCategoryButton.Visibility = Visibility.Visible
        Else
            itemGridView.IsItemClickEnabled = True
            itemGridView.SelectionMode = ListViewSelectionMode.None
            EditCategoryButton.Visibility = Visibility.Collapsed
            NewCategoryButton.Visibility = Visibility.Collapsed
        End If
    End Sub

    Private Async Sub EditCategory_Click(sender As Object, e As RoutedEventArgs) Handles EditCategory.Click
        If itemGridView.SelectedItem Is Nothing Then
            Dim msg = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("SelectACategory"))
            Await msg.ShowAsync()
            Return
        End If

        Dim selectedCategory = DirectCast(itemGridView.SelectedItem, RecipeFolder)

        Dim editor = New DefineCategoryDialog(selectedCategory.Name)
        Await editor.ShowAsync()

    End Sub

    Private Async Sub NewCategory_Click(sender As Object, e As RoutedEventArgs)

        Dim editor = New DefineCategoryDialog()
        Await editor.ShowAsync()

    End Sub

#End Region

#Region "Navigation"

    Private Sub CategorySelected(sender As Object, e As ItemClickEventArgs) Handles itemGridView.ItemClick
        If e.ClickedItem IsNot Nothing Then
            Dim folders = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)
            If folders IsNot Nothing Then
                Dim category = (DirectCast(e.ClickedItem, RecipeFolder))
                RootSplitView.IsPaneOpen = False
                Me.Frame.Navigate(GetType(RecipesPage), category.Name)
            End If
        End If
    End Sub

    Private Sub ShowFavorites_Click(sender As Object, e As RoutedEventArgs)
        RootSplitView.IsPaneOpen = False
        Me.Frame.Navigate(GetType(RecipesPage), Favorites.FolderName)
    End Sub
#End Region

#Region "Timers"
    Private Sub ShowTimers_Click(sender As Object, e As RoutedEventArgs) Handles ShowTimers.Click
        Dim newSetting As Boolean = Not TimerController.TimersPaneOpen
        TimerController.TimersPaneOpen = newSetting
    End Sub
#End Region

#Region "History"
    Private Sub ShowHistory_Click(sender As Object, e As RoutedEventArgs) Handles ShowHistory.Click
        Me.Frame.Navigate(GetType(RecipesPage), History.FolderName)

    End Sub
#End Region

#Region "AppSettings"
    Public ChangeRootFolderRequested As Boolean
    Private Async Sub AppSettings_Click(sender As Object, e As RoutedEventArgs) Handles AppSettings.Click
        ChangeRootFolderRequested = False
        Dim settingsDialog = New SettingsDialog
        Await settingsDialog.ShowAsync()
        If ChangeRootFolderRequested Then
            Await ChangeRootFolder()
        End If
    End Sub
#End Region

End Class

