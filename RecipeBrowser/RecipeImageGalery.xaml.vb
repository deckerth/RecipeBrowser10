' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports Windows.ApplicationModel.DataTransfer
Imports Windows.Foundation.Metadata
Imports Windows.Storage
Imports Windows.UI.Core
Imports Windows.UI.Popups
''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class RecipeImageGalery
    Inherits Page
    Implements INotifyPropertyChanged

#Region "Properties"

    Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Private Sub TimersPanePropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs)
        If e.PropertyName = "TimersPaneOpen" Then
            ShowTimersPaneButtonVisibility = Timers.Controller.Current.ShowTimersPaneButtonVisibility
            OnPropertyChanged("ShowTimersPaneButtonVisibility")
        End If
    End Sub

    Protected Sub OnPropertyChanged(ByVal PropertyName As String)
        ' Raise the event, and make this procedure
        ' overridable, should someone want to inherit from
        ' this class and override this behavior:
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
    End Sub

    Private _recipeImages As New ObservableCollection(Of RecipeImage)
    Public Property RecipeImages As ObservableCollection(Of RecipeImage)
        Get
            Return _recipeImages
        End Get
        Set(value As ObservableCollection(Of RecipeImage))
            Dim valueChange As Boolean
            If _recipeImages.Count = 0 And value.Count <> 0 Or _recipeImages.Count <> 0 And value.Count = 0 Then
                valueChange = True
            End If
            _recipeImages = value
            OnPropertyChanged("RecipeImages")
            If valueChange Then
                OnPropertyChanged("AddFirstRecipeImageTextVisibility")
                OnPropertyChanged("GaleryVisibility")
            End If
        End Set
    End Property


    Dim _AddFirstRecipeImageTextVisibility As Visibility
    Public Property AddFirstRecipeImageTextVisibility As Visibility
        Get
            Return _AddFirstRecipeImageTextVisibility
        End Get
        Set(value As Visibility)
            If value <> _AddFirstRecipeImageTextVisibility Then
                _AddFirstRecipeImageTextVisibility = value
                OnPropertyChanged("AddFirstRecipeImageTextVisibility")
            End If
        End Set
    End Property

    Public ReadOnly Property GaleryVisibility As Visibility
        Get
            If RecipeImages.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Collapsed
            End If
        End Get
    End Property

    ''' <summary>
    ''' NavigationHelper wird auf jeder Seite zur Unterstützung bei der Navigation verwendet und 
    ''' Verwaltung der Prozesslebensdauer
    ''' </summary>
    Public ReadOnly Property NavigationHelper As Common.NavigationHelper
        Get
            Return Me._navigationHelper
        End Get
    End Property

    Public Property TimerController As Timers.Controller

    Public Property ShowTimersPaneButtonVisibility As Visibility
#End Region

    Private _navigationHelper As Common.NavigationHelper

    Private CurrentRecipeFolder As RecipeFolder
    Private CurrentRecipe As Recipe
    Private MainCategory As String
    Private MaxRecipeIndex As Integer

    Enum CameraStates
        undefined
        present
        notPresent
    End Enum

    Private CameraAvailable As CameraStates

    Public Sub New()
        InitializeComponent()
        Me._navigationHelper = New Common.NavigationHelper(Me)
        AddHandler Me._navigationHelper.LoadState, AddressOf NavigationHelper_LoadState
        AddHandler Me._navigationHelper.SaveState, AddressOf NavigationHelper_SaveState

        TimerController = Timers.Controller.Current
        AddHandler Timers.Controller.Current.PropertyChanged, AddressOf TimersPanePropertyChanged

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

    Private Async Function RefreshDisplay() As Task
        DisableControls()
        Await CurrentRecipeFolder.GetImagesOfRecipeAsync(CurrentRecipe)
        RecipeImages = CurrentRecipe.Pictures
        MaxRecipeIndex = 0
        For Each item In RecipeImages
            Dim pos = item.File.Name.LastIndexOf("_")  ' name_image_00.png  len = 17 pos = 11
            If pos > 0 Then
                Dim numstr = item.File.Name.Substring(pos + 1)
                pos = numstr.LastIndexOfAny("012345689") ' 00.png  len = 6 pos = 1
                If pos >= 0 Then
                    numstr = numstr.Substring(0, pos + 1)
                    Dim index As Integer
                    Integer.TryParse(numstr, index)
                    If index > MaxRecipeIndex Then
                        MaxRecipeIndex = index
                    End If
                End If
            End If
        Next
        ImageGalery.ItemsSource = RecipeImages

        EnableControls()

        If RecipeImages.Count = 0 Then
            AddFirstRecipeImageTextVisibility = Visibility.Visible
        Else
            AddFirstRecipeImageTextVisibility = Visibility.Collapsed
        End If


    End Function

    Private Async Sub NavigationHelper_LoadState(sender As Object, e As Common.LoadStateEventArgs)

        Dim categories = DirectCast(App.Current.Resources("recipeFolders"), RecipeFolders)

        Dim key As String
        Dim category As String
        Dim name As String

        DisableControls()

        If CameraAvailable = CameraStates.undefined Then
            CameraAvailable = If((Await CameraPage.FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back)) Is Nothing, CameraStates.notPresent, CameraStates.present)
        End If

        If CameraAvailable = CameraStates.present Then
            TakePicture.Visibility = Visibility.Visible
        Else
            TakePicture.Visibility = Visibility.Collapsed
        End If

        ShowTimersPaneButtonVisibility = Timers.Controller.Current.ShowTimersPaneButtonVisibility

        key = DirectCast(e.NavigationParameter, String)
        Recipe.GetCategoryAndNameFromKey(key, MainCategory, category, name)

        CurrentRecipeFolder = Await categories.GetFolderAsync(category)

        pageTitle.Text = name

        CurrentRecipe = Await categories.GetRecipeAsync(MainCategory, category, name)

        If CurrentRecipe Is Nothing Then
            RecipeImages = Nothing
            EnableControls()
        Else
            Await RefreshDisplay()
        End If
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

#Region "EnableDisableControls"
    Private Sub DisableControls()

        ActivityIndicator.Visibility = Visibility.Visible
        AddFirstRecipeImageTextVisibility = Visibility.Collapsed

        RefreshGalery.IsEnabled = False
        TakePicture.IsEnabled = False
        UploadPicture.IsEnabled = False
        DeletePicture.IsEnabled = False
        SharePicture.IsEnabled = False

    End Sub

    Private Sub EnableControls()

        ActivityIndicator.Visibility = Visibility.Collapsed

        RefreshGalery.IsEnabled = True
        TakePicture.IsEnabled = True
        UploadPicture.IsEnabled = True
        DeletePicture.IsEnabled = True
        SharePicture.IsEnabled = True

    End Sub
#End Region

#Region "Upload"

    Public Shared Function GetFilenameForImage(recipeName As String, index As Integer) As String

        Dim indexStr As String = (index + 1).ToString
        If indexStr.Length = 1 Then
            indexStr = "0" + indexStr
        End If
        Return recipeName + "_image_" + indexStr

    End Function

    Private Async Sub UploadPicture_Click(sender As Object, e As RoutedEventArgs) Handles UploadPicture.Click

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
            Try
                Dim extension As String = String.Empty
                If file.Name.Length > 4 Then
                    extension = file.Name.Substring(file.Name.Length - 4)  ' x.jpg length = 5 startindex = 1
                End If
                Dim filename = GetFilenameForImage(CurrentRecipe.Name, MaxRecipeIndex) + extension
                Await file.CopyAsync(CurrentRecipeFolder.Folder, filename, NameCollisionOption.ReplaceExisting)
                Await RefreshDisplay()
                ImageGalery.SelectedIndex = RecipeImages.Count - 1
            Catch ex As Exception
            End Try
        End If

    End Sub
#End Region

#Region "Delete"
    Private Cancelled As Boolean

    Private Async Sub DeletePicture_Click(sender As Object, e As RoutedEventArgs) Handles DeletePicture.Click

        If ImageGalery.SelectedItem Is Nothing Then
            Return
        End If
        Dim currentImage As RecipeImage = ImageGalery.SelectedItem

        Dim messageDialog = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("DoYouWantToDeleteImage"))

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

        Try
            Await currentImage.File.DeleteAsync()
        Catch ex As Exception
            Cancelled = True
        End Try

        If Cancelled Then
            messageDialog = New MessageDialog(App.Texts.GetString("ImageNotDeleted"))
        Else
            Await RefreshDisplay()
        End If

    End Sub

#End Region

#Region "Share"
    Private Sub SharePicture_Click(sender As Object, e As RoutedEventArgs) Handles SharePicture.Click
        DataTransfer.DataTransferManager.ShowShareUI()
    End Sub

    Private Sub DataRequestedManager(sender As DataTransferManager, args As DataRequestedEventArgs)

        ' Share a recipe

        If ImageGalery.SelectedItem Is Nothing Then
            Return
        End If
        Dim currentImage As RecipeImage = ImageGalery.SelectedItem

        Dim request = args.Request
        Dim storageItems As New List(Of IStorageItem)

        storageItems.Add(currentImage.File)

        request.Data.Properties.Title = App.Texts.GetString("RecipeImage")
        request.Data.Properties.Description = currentImage.File.Name
        request.Data.SetStorageItems(storageItems)

    End Sub

#End Region

#Region "Refresh"
    Private Async Sub RefreshGalery_Click(sender As Object, e As RoutedEventArgs) Handles RefreshGalery.Click
        Await RefreshDisplay()
    End Sub
#End Region

#Region "Timer"
    'Private Sub ShowTimers_Click(sender As Object, e As RoutedEventArgs) Handles ShowTimers.Click
    '    TimerController.TimersPaneOpen = Not TimerController.TimersPaneOpen
    'End Sub

    Private Sub ShowTimersPane_Click(sender As Object, e As RoutedEventArgs) Handles ShowTimersPane.Click
        TimerController.TimersPaneOpen = Not TimerController.TimersPaneOpen
    End Sub
#End Region

#Region "Camera"
    Private Sub TakePicture_Click(sender As Object, e As RoutedEventArgs) Handles TakePicture.Click

        Frame.Navigate(GetType(CameraPage), CurrentRecipe.GetKey(MainCategory))

    End Sub
#End Region
End Class
