' Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

Imports Windows.Devices.Sensors
Imports Windows.Foundation.Metadata
Imports Windows.Media
Imports Windows.Media.Capture
Imports Windows.Media.MediaProperties
Imports Windows.Storage
Imports Windows.Storage.Streams
Imports Windows.System.Display
Imports Windows.UI.Core
Imports Windows.Devices.Enumeration
Imports Windows.Graphics.Imaging
Imports Windows.Phone.UI.Input
Imports Windows.Storage.FileProperties

''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class CameraPage
    Inherits Page

#Region "Properties"

    ''' <summary>
    ''' NavigationHelper wird auf jeder Seite zur Unterstützung bei der Navigation verwendet und 
    ''' Verwaltung der Prozesslebensdauer
    ''' </summary>
    Public ReadOnly Property NavigationHelper As Common.NavigationHelper
        Get
            Return Me._navigationHelper
        End Get
    End Property

#End Region

    Private _navigationHelper As Common.NavigationHelper

    Private CurrentRecipeFolder As RecipeFolder
    Private CurrentRecipe As Recipe

    ' Receive notifications about rotation of the device and UI and apply any necessary rotation to the preview stream and UI controls       
    Private ReadOnly _displayInformation As DisplayInformation = DisplayInformation.GetForCurrentView()

    Private ReadOnly _orientationSensor As SimpleOrientationSensor = SimpleOrientationSensor.GetDefault()

    Private _deviceOrientation As SimpleOrientation = SimpleOrientation.NotRotated

    Private _displayOrientation As DisplayOrientations = DisplayOrientations.Portrait

    ' Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
    ' Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
    Private Shared ReadOnly RotationKey As Guid = New Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1")

    ' Prevent the screen from sleeping while the camera is running
    Private ReadOnly _displayRequest As DisplayRequest = New DisplayRequest()

    ' For listening to media property changes
    Private ReadOnly _systemMediaControls As SystemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView()

    ' MediaCapture and its state variables
    Private _mediaCapture As MediaCapture

    Private _isInitialized As Boolean

    Private _isPreviewing As Boolean

    ' Information about the camera device
    Private _mirroringPreview As Boolean

    Private _externalCamera As Boolean

#Region " Constructor, lifecycle and navigation "
    Public Sub New()
        InitializeComponent()
        Me._navigationHelper = New Common.NavigationHelper(Me)
        AddHandler Me._navigationHelper.LoadState, AddressOf NavigationHelper_LoadState
        AddHandler Me._navigationHelper.SaveState, AddressOf NavigationHelper_SaveState

        NavigationCacheMode = NavigationCacheMode.Disabled
        AddHandler Application.Current.Suspending, AddressOf Application_Suspending
        AddHandler Application.Current.Resuming, AddressOf Application_Resuming
    End Sub

    Private Async Sub Application_Suspending(sender As Object, e As SuspendingEventArgs)
        If Frame.CurrentSourcePageType Is GetType(MainPage) Then
            Dim deferral = e.SuspendingOperation.GetDeferral()
            Await CleanupCameraAsync()
            Await CleanupUiAsync()
            deferral.Complete()
        End If
    End Sub

    Private Async Sub Application_Resuming(sender As Object, o As Object)
        If Frame.CurrentSourcePageType Is GetType(MainPage) Then
            Await SetupUiAsync()
            Await InitializeCameraAsync()
        End If
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

        CurrentRecipeFolder = Await categories.GetFolderAsync(category)
        CurrentRecipe = Await categories.GetRecipeAsync(folder, category, name)
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
#End Region

#Region "NavigationHelper-Registrierung"

    ''' Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
    ''' damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
    ''' 
    ''' Platzieren Sie seitenspezifische Logik in Ereignishandlern für  
    ''' <see cref="Common.NavigationHelper.LoadState"/>
    ''' and <see cref="Common.NavigationHelper.SaveState"/>.
    ''' Der Navigationsparameter ist in der LoadState-Methode verfügbar 
    ''' zusätzlich zum Seitenzustand, der während einer früheren Sitzung beibehalten wurde.

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        Dim currentView = SystemNavigationManager.GetForCurrentView()

        If Not ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons") Then
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
        Else
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
        End If

        _navigationHelper.OnNavigatedTo(e)
        Timers.Controller.Current.TimersPaneOpen = False

        Await SetupUiAsync()
        Await InitializeCameraAsync()
    End Sub


    Protected Overrides Async Sub OnNavigatedFrom(e As NavigationEventArgs)
        _navigationHelper.OnNavigatedFrom(e)
        Await CleanupCameraAsync()
        Await CleanupUiAsync()
    End Sub

#End Region

#Region " Event handlers "

    ''' <summary>
    ''' In the event of the app being minimized this method handles media property change events. If the app receives a mute
    ''' notification, it is no longer in the foregroud.
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="args"></param>
    Private Async Sub SystemMediaControls_PropertyChanged(sender As SystemMediaTransportControls, args As SystemMediaTransportControlsPropertyChangedEventArgs)
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Async Sub()
                                                                     If args.Property = SystemMediaTransportControlsProperty.SoundLevel AndAlso Frame.CurrentSourcePageType Is GetType(MainPage) Then
                                                                         If sender.SoundLevel = SoundLevel.Muted Then
                                                                             Await CleanupCameraAsync()
                                                                         ElseIf Not _isInitialized Then
                                                                             Await InitializeCameraAsync()
                                                                         End If
                                                                     End If
                                                                 End Sub)
    End Sub

    ''' <summary>
    ''' Occurs each time the simple orientation sensor reports a new sensor reading.
    ''' </summary>
    ''' <param name="sender">The event source.</param>
    ''' <param name="args">The event data.</param>
    Private Async Sub OrientationSensor_OrientationChanged(sender As SimpleOrientationSensor, args As SimpleOrientationSensorOrientationChangedEventArgs)
        If args.Orientation <> SimpleOrientation.Faceup AndAlso args.Orientation <> SimpleOrientation.Facedown Then
            _deviceOrientation = args.Orientation
            Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub() UpdateButtonOrientation())
        End If
    End Sub

    ''' <summary>
    ''' This event will fire when the page is rotated, when the DisplayInformation.AutoRotationPreferences value set in the SetupUiAsync() method cannot be not honored.
    ''' </summary>
    ''' <param name="sender">The event source.</param>
    ''' <param name="args">The event data.</param>
    Private Async Sub DisplayInformation_OrientationChanged(sender As DisplayInformation, args As Object)
        _displayOrientation = sender.CurrentOrientation
        If _isPreviewing Then
            Await SetPreviewRotationAsync()
        End If

        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub() UpdateButtonOrientation())
    End Sub

    Private Async Sub HardwareButtons_CameraPressed(sender As Object, e As CameraEventArgs)
        Await TakePhotoAsync()
    End Sub

    Private Async Sub MediaCapture_RecordLimitationExceeded(sender As MediaCapture)
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub() UpdateCaptureControls())
    End Sub

    Private Async Sub MediaCapture_Failed(sender As MediaCapture, errorEventArgs As MediaCaptureFailedEventArgs)
        Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message)
        Await CleanupCameraAsync()
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub() UpdateCaptureControls())
    End Sub

#End Region

#Region " MediaCapture methods "

    ''' <summary>
    ''' Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
    ''' </summary>
    ''' <returns></returns>
    Private Async Function InitializeCameraAsync() As Task
        Debug.WriteLine("InitializeCameraAsync")
        If _mediaCapture Is Nothing Then
            ' Attempt to get the back camera if one is available, but use any camera device if not
            Dim cameraDevice = Await FindCameraDeviceByPanelAsync(Panel.Back)
            If cameraDevice Is Nothing Then
                Debug.WriteLine("No camera device found!")
                Return
            End If

            _mediaCapture = New MediaCapture()
            AddHandler _mediaCapture.RecordLimitationExceeded, AddressOf MediaCapture_RecordLimitationExceeded
            AddHandler _mediaCapture.Failed, AddressOf MediaCapture_Failed
            Dim settings = New MediaCaptureInitializationSettings With {.VideoDeviceId = cameraDevice.Id}
            Try
                Await _mediaCapture.InitializeAsync(settings)
                _isInitialized = True
            Catch ex As UnauthorizedAccessException
                Debug.WriteLine("The app was denied access to the camera")
            End Try

            If _isInitialized Then
                If cameraDevice.EnclosureLocation Is Nothing OrElse cameraDevice.EnclosureLocation.Panel = Windows.Devices.Enumeration.Panel.Unknown Then
                    _externalCamera = True
                Else
                    _externalCamera = False
                    _mirroringPreview = (cameraDevice.EnclosureLocation.Panel = Windows.Devices.Enumeration.Panel.Front)
                End If

                Await StartPreviewAsync()
                UpdateCaptureControls()
            End If
        End If
    End Function

    ''' <summary>
    ''' Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
    ''' </summary>
    ''' <returns></returns>
    Private Async Function StartPreviewAsync() As Task
        _displayRequest.RequestActive()
        PreviewControl.Source = _mediaCapture
        PreviewControl.FlowDirection = If(_mirroringPreview, FlowDirection.RightToLeft, FlowDirection.LeftToRight)
        Await _mediaCapture.StartPreviewAsync()
        _isPreviewing = True
        If _isPreviewing Then
            Await SetPreviewRotationAsync()
        End If
    End Function

    ''' <summary>
    ''' Gets the current orientation of the UI in relation to the device (when AutoRotationPreferences cannot be honored) and applies a corrective rotation to the preview
    ''' </summary>
    Private Async Function SetPreviewRotationAsync() As Task
        If _externalCamera Then
            Return
        End If

        ' Calculate which way and how far to rotate the preview
        Dim rotationDegrees As Integer = ConvertDisplayOrientationToDegrees(_displayOrientation)
        If _mirroringPreview Then
            rotationDegrees = (360 - rotationDegrees) Mod 360
        End If

        ' Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
        Dim props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview)
        props.Properties.Add(RotationKey, rotationDegrees)
        Await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, Nothing)
    End Function

    ''' <summary>
    ''' Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
    ''' </summary>
    ''' <returns></returns>
    Private Async Function StopPreviewAsync() As Task
        _isPreviewing = False
        Await _mediaCapture.StopPreviewAsync()
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub()
                                                                     PreviewControl.Source = Nothing
                                                                     _displayRequest.RequestRelease()
                                                                 End Sub)
    End Function

    ''' <summary>
    ''' Takes a photo to a StorageFile and adds rotation metadata to it
    ''' </summary>
    ''' <returns></returns>
    Private Async Function TakePhotoAsync() As Task
        Dim stream = New InMemoryRandomAccessStream()
        Try
            Debug.WriteLine("Taking photo...")
            Await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream)
            Debug.WriteLine("Photo taken!")
            Dim photoOrientation = ConvertOrientationToPhotoOrientation(GetCameraOrientation())
            Await ReencodeAndSavePhotoAsync(stream, photoOrientation)
        Catch ex As Exception
            Debug.WriteLine("Exception when taking a photo: {0}", ex.ToString())
        End Try
    End Function

    ''' <summary>
    ''' Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
    ''' </summary>
    ''' <returns></returns>
    Private Async Function CleanupCameraAsync() As Task
        Debug.WriteLine("CleanupCameraAsync")
        If _isInitialized Then
            If _isPreviewing Then
                Await StopPreviewAsync()
            End If

            _isInitialized = False
        End If

        If _mediaCapture IsNot Nothing Then
            RemoveHandler _mediaCapture.RecordLimitationExceeded, AddressOf MediaCapture_RecordLimitationExceeded
            RemoveHandler _mediaCapture.Failed, AddressOf MediaCapture_Failed
            _mediaCapture.Dispose()
            _mediaCapture = Nothing
        End If
    End Function

#End Region

#Region "Camera Helper functions "

    ''' <summary>
    ''' Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
    ''' </summary>
    ''' <returns></returns>
    Private Async Function SetupUiAsync() As Task
        DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape
        If ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar") Then
            Await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync()
        End If

        _displayOrientation = _displayInformation.CurrentOrientation
        If _orientationSensor IsNot Nothing Then
            _deviceOrientation = _orientationSensor.GetCurrentOrientation()
        End If

        RegisterEventHandlers()
    End Function

    ''' <summary>
    ''' Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
    ''' </summary>
    ''' <returns></returns>
    Private Async Function CleanupUiAsync() As Task
        UnregisterEventHandlers()
        If ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar") Then
            Await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ShowAsync()
        End If

        DisplayInformation.AutoRotationPreferences = DisplayOrientations.None
    End Function

    ''' <summary>
    ''' This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
    ''' </summary>
    Private Sub UpdateCaptureControls()
        PhotoButton.IsEnabled = _isPreviewing
        If _isInitialized AndAlso Not _mediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported Then
            PhotoButton.IsEnabled = True
            PhotoButton.Opacity = If(PhotoButton.IsEnabled, 1, 0)
        End If
    End Sub

    ''' <summary>
    ''' Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
    ''' </summary>
    Private Sub RegisterEventHandlers()
        If ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons") Then
            AddHandler HardwareButtons.CameraPressed, AddressOf HardwareButtons_CameraPressed
        End If

        If _orientationSensor IsNot Nothing Then
            AddHandler _orientationSensor.OrientationChanged, AddressOf OrientationSensor_OrientationChanged
            UpdateButtonOrientation()
        End If

        AddHandler _displayInformation.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        AddHandler _systemMediaControls.PropertyChanged, AddressOf SystemMediaControls_PropertyChanged
    End Sub

    ''' <summary>
    ''' Unregisters event handlers for hardware buttons and orientation sensors
    ''' </summary>
    Private Sub UnregisterEventHandlers()
        If ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons") Then
            RemoveHandler HardwareButtons.CameraPressed, AddressOf HardwareButtons_CameraPressed
        End If

        If _orientationSensor IsNot Nothing Then
            RemoveHandler _orientationSensor.OrientationChanged, AddressOf OrientationSensor_OrientationChanged
        End If

        RemoveHandler _displayInformation.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        RemoveHandler _systemMediaControls.PropertyChanged, AddressOf SystemMediaControls_PropertyChanged
    End Sub

    ''' <summary>
    ''' Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
    ''' </summary>
    ''' <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
    ''' <returns></returns>
    Public Shared Async Function FindCameraDeviceByPanelAsync(desiredPanel As Windows.Devices.Enumeration.Panel) As Task(Of DeviceInformation)
        ' Get available devices for capturing pictures
        Dim allVideoDevices = Await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)
        ' Get the desired camera by panel
        Dim desiredDevice As DeviceInformation = allVideoDevices.FirstOrDefault(Function(x) x.EnclosureLocation IsNot Nothing AndAlso x.EnclosureLocation.Panel = desiredPanel)
        Return If(desiredDevice, allVideoDevices.FirstOrDefault())
    End Function

    ''' <summary>
    ''' Applies the given orientation to a photo stream and saves it as a StorageFile
    ''' </summary>
    ''' <param name="stream">The photo stream</param>
    ''' <param name="photoOrientation">The orientation metadata to apply to the photo</param>
    ''' <returns></returns>
    Private Async Function ReencodeAndSavePhotoAsync(stream As IRandomAccessStream, photoOrientation As PhotoOrientation) As Task
        Using inputStream = stream
            Dim decoder = Await BitmapDecoder.CreateAsync(inputStream)
            Dim filename = Await GetFilenameForNewImageAsync(".jpg")
            Dim file = Await CurrentRecipeFolder.Folder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName)
            Using outputStream = Await file.OpenAsync(FileAccessMode.ReadWrite)
                Dim encoder = Await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder)
                Dim properties = New BitmapPropertySet From {{"System.Photo.Orientation", New BitmapTypedValue(photoOrientation, PropertyType.UInt16)}}
                Await encoder.BitmapProperties.SetPropertiesAsync(properties)
                Await encoder.FlushAsync()
            End Using
        End Using
    End Function

#End Region

#Region " Rotation helpers "

    ''' <summary>
    ''' Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
    ''' </summary>
    ''' <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
    Private Function GetCameraOrientation() As SimpleOrientation
        If _externalCamera Then
            Return SimpleOrientation.NotRotated
        End If

        Dim result = _deviceOrientation
        If _displayInformation.NativeOrientation = DisplayOrientations.Portrait Then
            Select Case result
                Case SimpleOrientation.Rotated90DegreesCounterclockwise
                    result = SimpleOrientation.NotRotated
                Case SimpleOrientation.Rotated180DegreesCounterclockwise
                    result = SimpleOrientation.Rotated90DegreesCounterclockwise
                Case SimpleOrientation.Rotated270DegreesCounterclockwise
                    result = SimpleOrientation.Rotated180DegreesCounterclockwise
                Case SimpleOrientation.NotRotated
                    result = SimpleOrientation.Rotated270DegreesCounterclockwise
            End Select
        End If

        If _mirroringPreview Then
            Select Case result
                Case SimpleOrientation.Rotated90DegreesCounterclockwise
                    Return SimpleOrientation.Rotated270DegreesCounterclockwise
                Case SimpleOrientation.Rotated270DegreesCounterclockwise
                    Return SimpleOrientation.Rotated90DegreesCounterclockwise
            End Select
        End If

        Return result
    End Function

    ''' <summary>
    ''' Converts the given orientation of the device in space to the corresponding rotation in degrees
    ''' </summary>
    ''' <param name="orientation">The orientation of the device in space</param>
    ''' <returns>An orientation in degrees</returns>
    Private Shared Function ConvertDeviceOrientationToDegrees(orientation As SimpleOrientation) As Integer
        Select Case orientation
            Case SimpleOrientation.Rotated90DegreesCounterclockwise
                Return 90
            Case SimpleOrientation.Rotated180DegreesCounterclockwise
                Return 180
            Case SimpleOrientation.Rotated270DegreesCounterclockwise
                Return 270
            Case Else
                Return 0
        End Select
    End Function

    ''' <summary>
    ''' Converts the given orientation of the app on the screen to the corresponding rotation in degrees
    ''' </summary>
    ''' <param name="orientation">The orientation of the app on the screen</param>
    ''' <returns>An orientation in degrees</returns>
    Private Shared Function ConvertDisplayOrientationToDegrees(orientation As DisplayOrientations) As Integer
        Select Case orientation
            Case DisplayOrientations.Portrait
                Return 90
            Case DisplayOrientations.LandscapeFlipped
                Return 180
            Case DisplayOrientations.PortraitFlipped
                Return 270
            Case Else
                Return 0
        End Select
    End Function

    ''' <summary>
    ''' Converts the given orientation of the device in space to the metadata that can be added to captured photos
    ''' </summary>
    ''' <param name="orientation">The orientation of the device in space</param>
    ''' <returns></returns>
    Private Shared Function ConvertOrientationToPhotoOrientation(orientation As SimpleOrientation) As PhotoOrientation
        Select Case orientation
            Case SimpleOrientation.Rotated90DegreesCounterclockwise
                Return PhotoOrientation.Rotate90
            Case SimpleOrientation.Rotated180DegreesCounterclockwise
                Return PhotoOrientation.Rotate180
            Case SimpleOrientation.Rotated270DegreesCounterclockwise
                Return PhotoOrientation.Rotate270
            Case Else
                Return PhotoOrientation.Normal
        End Select
    End Function

    ''' <summary>
    ''' Uses the current device orientation in space and page orientation on the screen to calculate the rotation
    ''' transformation to apply to the controls
    ''' </summary>
    Private Sub UpdateButtonOrientation()
        Dim device As Integer = ConvertDeviceOrientationToDegrees(_deviceOrientation)
        Dim display As Integer = ConvertDisplayOrientationToDegrees(_displayOrientation)
        If _displayInformation.NativeOrientation = DisplayOrientations.Portrait Then
            device -= 90
        End If

        ' Combine both rotations and make sure that 0 <= result < 360
        Dim angle = (360 + display + device) Mod 360
        ' Rotate the buttons in the UI to match the rotation of the device
        Dim transform = New RotateTransform With {.Angle = angle}
        PhotoButton.RenderTransform = transform
    End Sub
#End Region

#Region "Helper"
    Private Async Function GetMaxRecipeIndex() As Task(Of Integer)
        Dim MaxRecipeIndex As Integer

        Await CurrentRecipeFolder.GetImagesOfRecipeAsync(CurrentRecipe)
        MaxRecipeIndex = 0
        For Each item In CurrentRecipe.Pictures
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
        Return MaxRecipeIndex
    End Function

    Private Async Function GetFilenameForNewImageAsync(extension As String) As Task(Of String)

        Dim maxRecipeIndex As Integer = Await GetMaxRecipeIndex()
        Return RecipeImageGalery.GetFilenameForImage(CurrentRecipe.Name, maxRecipeIndex) + extension

    End Function

#End Region

#Region "Photo"
    Private Async Sub PhotoButton_Tapped(sender As Object, e As TappedRoutedEventArgs)
        Await TakePhotoAsync()
    End Sub

#End Region

End Class
