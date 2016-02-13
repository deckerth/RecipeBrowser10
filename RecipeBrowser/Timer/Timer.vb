Imports Windows.Storage

Namespace Global.Timers

    Public Class Timer

        Implements INotifyPropertyChanged

        Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

        Protected Overridable Sub OnPropertyChanged(ByVal PropertyName As String)
            ' Raise the event, and make this procedure
            ' overridable, should someone want to inherit from
            ' this class and override this behavior:
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
        End Sub

        Public Event TimeIsUp()

        Private Enum TimerState
            Stopped
            Ready
            Running
            Paused
        End Enum

        Private _State As TimerState = TimerState.Stopped
        Private _DisplayTime As String = "  00:00:00"
        Private _DisplayTimeForeground As SolidColorBrush = Timers.Factory.TimeNotOverForeground
        Private _StartAllowed As Visibility = Visibility.Collapsed
        Private _StopAllowed As Visibility = Visibility.Collapsed
        Private _ContinueAllowed As Visibility = Visibility.Collapsed
        Private _SetAllowed As Visibility = Visibility.Visible
        Private _TimerIndex As Integer
        Private _PickedTime As TimeSpan

        Private StartTime As DateTime
        Private SecondsToWait As Integer
        Private LastRenderedTime As Integer

#Region "Properties"
        Public Property TimerIndex As Integer
            Get
                Return _TimerIndex
            End Get
            Set(value As Integer)
                _TimerIndex = value
            End Set
        End Property

        Private Property State As TimerState
            Get
                Return _State
            End Get
            Set(value As TimerState)
                If value <> _State Then
                    _State = value
                    SetVisibilities()
                End If
            End Set
        End Property

        Public Property DisplayTime As String
            Get
                Return _DisplayTime
            End Get
            Set(value As String)
                If value <> _DisplayTime Then
                    _DisplayTime = value
                    OnPropertyChanged("DisplayTime")
                End If
            End Set
        End Property

        Public Property PickedTime As TimeSpan
            Get
                Return _PickedTime
            End Get
            Set(value As TimeSpan)
                If Not value.Equals(_PickedTime) Then
                    _PickedTime = value
                    OnPropertyChanged("PickedTime")
                End If
            End Set
        End Property

        Public Property DisplayTimeForeground As SolidColorBrush
            Get
                Return _DisplayTimeForeground
            End Get
            Set(value As SolidColorBrush)
                If Not value.Equals(_DisplayTimeForeground) Then
                    _DisplayTimeForeground = value
                    OnPropertyChanged("DisplayTimeForeground")
                End If
            End Set
        End Property

        Public Property StartAllowed As Visibility
            Get
                Return _StartAllowed
            End Get
            Set(value As Visibility)
                If value <> _StartAllowed Then
                    _StartAllowed = value
                    OnPropertyChanged("StartAllowed")
                End If
            End Set
        End Property

        Public Property StopAllowed As Visibility
            Get
                Return _StopAllowed
            End Get
            Set(value As Visibility)
                If value <> _StopAllowed Then
                    _StopAllowed = value
                    OnPropertyChanged("StopAllowed")
                End If
            End Set
        End Property

        Public Property ContinueAllowed As Visibility
            Get
                Return _ContinueAllowed
            End Get
            Set(value As Visibility)
                If value <> _ContinueAllowed Then
                    _ContinueAllowed = value
                    OnPropertyChanged("ContinueAllowed")
                End If
            End Set
        End Property

        Public Property SetAllowed As Visibility
            Get
                Return _SetAllowed
            End Get
            Set(value As Visibility)
                If value <> _SetAllowed Then
                    _SetAllowed = value
                    OnPropertyChanged("SetAllowed")
                End If
            End Set
        End Property
#End Region

        Public Sub New(index As Integer)
            TimerIndex = index

            Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
            Dim timerList As ApplicationDataContainer = localSettings.CreateContainer("Timers", Windows.Storage.ApplicationDataCreateDisposition.Always)
            Dim settingLoaded As Boolean

            If timerList.Values.Values.Count > index Then
                Dim timerSetting As ApplicationDataCompositeValue = timerList.Values(TimerIndex.ToString)
                If DateTime.TryParse(timerSetting("StartTime"), StartTime) Then
                    If Integer.TryParse(timerSetting("SecondsToWait"), SecondsToWait) Then
                        Dim storedState As Integer
                        If Integer.TryParse(timerSetting("State"), storedState) Then
                            settingLoaded = True
                            Select Case storedState
                                Case 0
                                    State = TimerState.Stopped
                                Case 1
                                    State = TimerState.Ready
                                    RenderTimeRemaining(SecondsToWait)
                                Case 2
                                    State = TimerState.Running
                                    Tick()
                                Case 3
                                    State = TimerState.Paused
                                    RenderTimeRemaining(SecondsToWait)
                                Case Else
                                    settingLoaded = False
                            End Select
                        End If
                    End If
                End If
            End If

            If Not settingLoaded Then
                DisplayTime = "  00:00:00"
                State = TimerState.Stopped
            End If

        End Sub

        Private Sub StoreState()

            Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
            Dim timerList As ApplicationDataContainer = localSettings.CreateContainer("Timers", Windows.Storage.ApplicationDataCreateDisposition.Always)

            Dim timerSetting = New Windows.Storage.ApplicationDataCompositeValue()
            timerSetting("StartTime") = StartTime.ToString
            timerSetting("SecondsToWait") = SecondsToWait.ToString
            Select Case State
                Case TimerState.Stopped
                    timerSetting("State") = "0"
                Case TimerState.Ready
                    timerSetting("State") = "1"
                Case TimerState.Running
                    timerSetting("State") = "2"
                Case TimerState.Paused
                    timerSetting("State") = "3"
            End Select
            timerList.Values(TimerIndex.ToString) = timerSetting

        End Sub

        Public Sub SetTimer(ByRef timeToWait As Double)
            If State <> TimerState.Stopped Or timeToWait < 1 Then
                Return
            End If

            SecondsToWait = Integer.Parse(timeToWait.ToString)
            State = TimerState.Ready
            RenderTimeRemaining(SecondsToWait)
            StoreState()
        End Sub

        Public Sub StartTimer()

            If State <> TimerState.Ready And State <> TimerState.Paused Then
                Return
            End If

            State = TimerState.Running
            StartTime = DateTime.Now
            Tick()
            StoreState()
        End Sub

        Public Sub PauseTimer()
            If State <> TimerState.Running Then
                Return
            End If

            Dim now = DateTime.Now
            Dim diff = now.Subtract(StartTime)
            SecondsToWait = SecondsToWait - diff.Seconds

            State = TimerState.Paused
            StoreState()
        End Sub

        Public Sub StopTimer()
            If State <> TimerState.Running Then
                Return
            End If
            State = TimerState.Stopped
            StoreState()
        End Sub

        Public Sub Tick()

            If State = TimerState.Running Then
                Dim now = DateTime.Now
                Dim diff = now.Subtract(StartTime)
                Dim remaining As Integer = SecondsToWait - diff.TotalSeconds
                Dim remainingBefore As Integer = LastRenderedTime
                RenderTimeRemaining(remaining)
                If remainingBefore > 0 And remaining <= 0 Then
                    RaiseEvent TimeIsUp()
                End If
            End If

        End Sub

#Region "Rendering"
        Private Sub SetVisibilities()

            Select Case State
                Case TimerState.Stopped
                    SetAllowed = Visibility.Visible
                    StartAllowed = Visibility.Collapsed
                    StopAllowed = Visibility.Collapsed
                    ContinueAllowed = Visibility.Collapsed
                Case TimerState.Ready
                    SetAllowed = Visibility.Collapsed
                    StartAllowed = Visibility.Visible
                    StopAllowed = Visibility.Collapsed
                    ContinueAllowed = Visibility.Collapsed
                Case TimerState.Running
                    SetAllowed = Visibility.Collapsed
                    StartAllowed = Visibility.Collapsed
                    StopAllowed = Visibility.Visible
                    ContinueAllowed = Visibility.Collapsed
                Case TimerState.Paused
                    SetAllowed = Visibility.Collapsed
                    StartAllowed = Visibility.Collapsed
                    StopAllowed = Visibility.Collapsed
                    ContinueAllowed = Visibility.Visible
            End Select
        End Sub

        Private Sub RenderTimeRemaining(ByVal remaining As Integer)

            LastRenderedTime = remaining

            Dim timeStr As String
            If remaining < 0 Then
                timeStr = "- "
                remaining = -remaining
                DisplayTimeForeground = Timers.Factory.TimeOverForeground
            Else
                DisplayTimeForeground = Timers.Factory.TimeNotOverForeground
                timeStr = "  "
            End If
            Dim hours As Integer = remaining \ 3600
            remaining = remaining - hours * 3600
            Dim minutes As Integer = remaining \ 60
            remaining = remaining - minutes * 60
            Dim hoursStr As String = hours.ToString
            Dim minutesStr As String = minutes.ToString
            Dim secondsStr As String = remaining.ToString
            If hours < 10 Then
                hoursStr = "0" + hoursStr
            End If
            If minutes < 10 Then
                minutesStr = "0" + minutesStr
            End If
            If remaining < 10 Then
                secondsStr = "0" + secondsStr
            End If
            DisplayTime = timeStr + hoursStr + ":" + minutesStr + ":" + secondsStr
        End Sub

#End Region

    End Class

End Namespace

