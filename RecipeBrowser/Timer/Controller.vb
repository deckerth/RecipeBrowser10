Namespace Global.Timers

    Public Class Controller
        Implements INotifyPropertyChanged

        Public Event PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

        Protected Overridable Sub OnPropertyChanged(ByVal PropertyName As String)
            ' Raise the event, and make this procedure
            ' overridable, should someone want to inherit from
            ' this class and override this behavior:
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
        End Sub

        Public Shared Current As Controller

        Public Sub New()
            Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings

            Current = Me
            ShowTimersButtonVisibility = Factory.Current.TimersAllowed
            Dim isOpen As String = localSettings.Values("TimersPaneOpen")
            If isOpen IsNot Nothing Then
                Boolean.TryParse(isOpen, TimersPaneOpen)
            End If
        End Sub

        Dim _ShowTimersButtonVisibility As Visibility
        Public Property ShowTimersButtonVisibility As Visibility
            Get
                Return _ShowTimersButtonVisibility
            End Get
            Set(value As Visibility)
                If value <> _ShowTimersButtonVisibility Then
                    _ShowTimersButtonVisibility = value
                    OnPropertyChanged("ShowTimersButtonVisibility")
                End If
            End Set
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
                    Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
                    localSettings.Values("TimersPaneOpen") = _TimersPaneOpen.ToString
                End If
            End Set
        End Property

    End Class

End Namespace
