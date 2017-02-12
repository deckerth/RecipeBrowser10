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
            Dim isOpen As String = localSettings.Values("TimersPaneOpen")
            If isOpen IsNot Nothing Then
                Boolean.TryParse(isOpen, TimersPaneOpen)
            End If
        End Sub

        Public Shared Sub CreateInstance()
            If Current Is Nothing Then
                Dim newInstance As Controller = New Controller()
            End If
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
                    Dim localSettings = Windows.Storage.ApplicationData.Current.LocalSettings
                    localSettings.Values("TimersPaneOpen") = _TimersPaneOpen.ToString
                    OnPropertyChanged("ShowTimersPaneButtonVisibility")
                End If
            End Set
        End Property

    End Class

End Namespace
