' Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

Imports Windows.Foundation.Metadata
Imports Windows.UI.Core
''' <summary>
''' Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page
    Implements INotifyPropertyChanged

    Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub OnPropertyChanged(ByVal PropertyName As String)
        ' Raise the event, and make this procedure
        ' overridable, should someone want to inherit from
        ' this class and override this behavior:
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
    End Sub

    Public ReadOnly Property ShowTimersPaneButtonVisibility As Visibility
        Get
            If _TimersPaneOpen Then
                Return Visibility.Collapsed
            Else
                Return Visibility.Visible
            End If
        End Get
    End Property

    Dim _TimersPaneOpen As Boolean
    Public Property TimersPaneOpen As Boolean
        Get
            Return _TimersPaneOpen
        End Get
        Set(value As Boolean)
            If value <> _TimersPaneOpen Then
                _TimersPaneOpen = value
                OnPropertyChanged("TimersPaneOpen")
                OnPropertyChanged("ShowTimersPaneButtonVisibility")
            End If
        End Set
    End Property

    Private Sub HandleTimersPropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs)
        If e.PropertyName.Equals("TimersPaneOpen") Then
            TimersPaneOpen = Timers.Controller.Current.TimersPaneOpen
        End If
    End Sub

    Public Property AllTimers As ObservableCollection(Of Timers.Timer)

    Public Shared ContentFrame As Frame

    Public Sub New()
        InitializeComponent()

        AllTimers = Timers.Factory.Current.Timers
        TimersPaneOpen = Timers.Controller.Current.TimersPaneOpen
        Timers.Factory.Current.BindPage(Dispatcher)
        AddHandler Timers.Factory.Current.TimeIsUp, AddressOf OnTimeIsUp
        AddHandler Timers.Factory.Current.StopRinging, AddressOf OnStopRinging
        AddHandler App.Current.Resuming, AddressOf OnAppResuming
        AddHandler Timers.Controller.Current.PropertyChanged, AddressOf HandleTimersPropertyChanged
        ContentFrame = MainFrame

        App.Logger.Write("MainPage created")
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        Dim currentView = SystemNavigationManager.GetForCurrentView()

        If Not ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons") Then
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
        Else
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
        End If

        MainFrame.Navigate(GetType(CategoryOverview))
    End Sub

    Private Sub OnAppResuming(sender As Object, e As Object)
        AllTimers = Timers.Factory.Current.Timers
        App.Logger.Write("Main page initialized, TimerController bound:" + (Timers.Controller.Current IsNot Nothing).ToString)
    End Sub

#Region "Timers"
    Private Sub HideTimers_Click(sender As Object, e As RoutedEventArgs) Handles HideTimers.Click
        Timers.Controller.Current.TimersPaneOpen = False
        Ringer.Stop()
    End Sub

    Private Sub OnTimeIsUp()
        Timers.Controller.Current.TimersPaneOpen = True
        Ringer.Play()
    End Sub

    Private Sub OnStopRinging()
        Ringer.Stop()
    End Sub
#End Region

End Class
