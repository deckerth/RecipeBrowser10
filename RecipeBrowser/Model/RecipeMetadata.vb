Public Class RecipeMetadata

    Const LastTimeCookedNode As String = "LastTimeCooked"
    Const LastCookedDate As String = "LastCookedDate"
    Const CookedNoOfTimes As String = "CookedNoOfTimes"

    Public Shared Instance As New RecipeMetadata

    Public Async Function ReadMetadataAsync(ByVal recipe As Recipe, Optional ByVal metaDataFile As Windows.Storage.StorageFile = Nothing) As Task

        recipe.LastCooked = ""
        recipe.CookedNoOfTimes = 0

        Try
            If metaDataFile Is Nothing Then
                Try
                    Dim parent = Await recipe.File.GetParentAsync()
                    metaDataFile = Await parent.GetFileAsync(recipe.Name + ".xml")
                Catch ex As Exception
                End Try
            End If

            If metaDataFile IsNot Nothing Then
                Dim xmlDoc = Await Windows.Data.Xml.Dom.XmlDocument.LoadFromFileAsync(metaDataFile)

                For Each element In xmlDoc.ChildNodes
                    Select Case element.NodeName
                        Case LastTimeCookedNode
                            For Each attribute In element.Attributes
                                Select Case attribute.NodeName
                                    Case LastCookedDate
                                        recipe.LastCooked = attribute.NodeValue
                                    Case CookedNoOfTimes
                                        recipe.CookedNoOfTimes = attribute.NodeValue
                                End Select
                            Next
                    End Select
                Next
            End If

        Catch ex As Exception
        End Try

        recipe.RenderSubTitle()

    End Function

    Public Async Function WriteMetadataAsync(ByVal folder As Windows.Storage.StorageFolder, ByVal recipe As Recipe) As Task

        Dim errorFlag As Boolean

        If recipe.CookedNoOfTimes = 0 Then
            Return
        End If

        Try
            Dim xmlfile = Await folder.CreateFileAsync(recipe.Name + ".xml", Windows.Storage.CreationCollisionOption.ReplaceExisting)

            Dim xmlDocument As New Windows.Data.Xml.Dom.XmlDocument

            Dim element = xmlDocument.CreateElement(LastTimeCookedNode)
            element.SetAttribute(LastCookedDate, recipe.LastCooked)
            element.SetAttribute(CookedNoOfTimes, recipe.CookedNoOfTimes)
            xmlDocument.AppendChild(element)

            Await xmlDocument.SaveToFileAsync(xmlfile)
        Catch ex As Exception
            errorFlag = True
        End Try

        If errorFlag Then
            Dim msg = New Windows.UI.Popups.MessageDialog(App.Texts.GetString("UnableToSaveNotes"))
            Await msg.ShowAsync()
        End If
    End Function

End Class
