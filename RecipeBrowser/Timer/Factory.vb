Imports Windows.ApplicationModel.Background
Imports Windows.System.Threading
Imports Windows.UI.Core
Imports Windows.UI.Popups

Namespace Global.Timers

    Public Class Factory

        Public Shared Current As Timers.Factory

        Public TimersAllowed As Boolean

        Private _Timers As New ObservableCollection(Of Timer)()
        Public ReadOnly Property Timers As ObservableCollection(Of Timer)
            Get
                Return _Timers
            End Get
        End Property

        Public Shared TimeOverForeground As SolidColorBrush
        Public Shared TimeNotOverForeground As SolidColorBrush

        Private Dispatcher As CoreDispatcher
        Private PeriodicTimer As ThreadPoolTimer
        Public Event TimeIsUp()
        Public Event StopRinging()

#Region "Initialization"

        Public Sub New()
            Current = Me

            TimeOverForeground = DirectCast(RecipeBrowser.App.Current.Resources("TimeOverForegroundBrush"), SolidColorBrush)
            TimeNotOverForeground = DirectCast(RecipeBrowser.App.Current.Resources("TimeNotOverForegroundBrush"), SolidColorBrush)
            AddHandler Application.Current.Resuming, AddressOf App_Resuming
            AddHandler Application.Current.Suspending, AddressOf App_Suspending
        End Sub

        Public Sub Initialize()
            PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer(New TimerElapsedHandler(AddressOf PeriodicTimerCallback), TimeSpan.FromSeconds(1))
            TimersAllowed = True

            Dim theController = New Timers.Controller()
        End Sub

        Public Sub BindPage(ByRef disp As CoreDispatcher)
            Dispatcher = disp
        End Sub

#End Region

#Region "Timer Control"
        Public Sub StopRinger()
            RaiseEvent StopRinging()
        End Sub

        Public Function CreateTimer() As Timers.Timer
            If Not TimersAllowed Then
                Return Nothing
            End If

            Dim t As New Timers.Timer(Timers.Count)
            Timers.Add(t)

            AddHandler t.TimeIsUp, AddressOf OnTimeIsUp

            Return t
        End Function

        Function GetTimer(index As Integer)
            If TimersAllowed = True And index < Timers.Count Then
                Return Timers(index)
            Else
                Return Nothing
            End If
        End Function

#End Region

#Region "Event Handler"
        Private Async Sub PeriodicTimerCallback(timer As ThreadPoolTimer)
            If Dispatcher IsNot Nothing Then
                Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Sub()
                                                                             For Each t In Timers
                                                                                 t.Tick()
                                                                             Next
                                                                         End Sub)
            End If
        End Sub

        Private Sub App_Suspending(sender As Object, e As SuspendingEventArgs)
            PeriodicTimer.Cancel()
        End Sub

        Private Sub App_Resuming(sender As Object, e As Object)
            Initialize()
        End Sub

        Private Sub OnTimeIsUp()
            RaiseEvent TimeIsUp()
        End Sub

#End Region

    End Class

End Namespace

