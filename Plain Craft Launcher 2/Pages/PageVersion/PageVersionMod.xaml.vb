﻿Imports System.Security.Principal
Imports PCL.MyLoading

Public Class PageVersionMod

#Region "初始化"

    Private IsLoad As Boolean = False
    Public Sub PageOther_Loaded() Handles Me.Loaded

        PanBack.ScrollToHome()
        AniControlEnabled += 1
        SelectedMods.Clear()
        RefreshList()
        ChangeAllSelected(False)
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoad Then Exit Sub
        IsLoad = True

#If DEBUG Then
        BtnManageCheck.Visibility = Visibility.Visible
#End If

    End Sub
    ''' <summary>
    ''' 刷新 Mod 列表。
    ''' </summary>
    Public Sub RefreshList(Optional ForceReload As Boolean = False)
        If McModLoader.State = LoadState.Loading Then Exit Sub
        If LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", If(ForceReload, LoaderFolderRunType.ForceRun, LoaderFolderRunType.RunOnUpdated)) Then
            Log("[System] 已刷新 Mod 列表")
            PanBack.ScrollToHome()
            SearchBox.Text = ""
        End If
    End Sub

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McModLoader, AddressOf Load_Finish, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McModLoader.State = LoadState.Failed Then
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.ForceRun)
        End If
    End Sub

#End Region

#Region "UI 化"

    Private PanItems As StackPanel
    ''' <summary>
    ''' 将 Mod 列表加载为 UI。
    ''' </summary>
    Private Sub Load_Finish(Loader As LoaderTask(Of String, List(Of McMod)))
        Dim List As List(Of McMod) = Loader.Output
        Try
            PanList.Children.Clear()

            '判断应该显示哪一个页面
            If List.Count = 0 Then
                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                Exit Sub
            Else
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            End If

            SearchBox.Text = ""
            '建立 StackPanel
            PanItems = New StackPanel With {.Margin = New Thickness(18, MyCard.SwapedHeight, 18, If(List.Count > 0, 20, 0)), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
            For Each ModEntity As McMod In List
                PanItems.Children.Add(McModListItem(ModEntity))
            Next
            '建立 MyCard
            Dim NewCard As New MyCard With {.Title = McModGetTitle(List), .Margin = New Thickness(0, 0, 0, 15)}
            NewCard.Children.Add(PanItems)
            PanList.Children.Add(NewCard)
            '显示提示
            If List.Count > 0 AndAlso Not Setup.Get("HintModDisable") Then
                Setup.Set("HintModDisable", True)
                Hint("直接点击某个 Mod 项即可将它禁用！")
            End If

        Catch ex As Exception
            Log(ex, "加载 Mod 列表 UI 失败", LogLevel.Feedback)
        End Try
    End Sub
    ''' <summary>
    ''' 获取 Card 的标题。
    ''' </summary>
    Private Function McModGetTitle(List As List(Of McMod)) As String
        Dim Counter = {0, 0, 0}
        For Each ModEntity As McMod In List
            Counter(ModEntity.State) += 1
        Next
        If List.Count = 0 Then Return "未找到任何 Mod"
        Dim TypeList As New List(Of String)
        If Counter(McMod.McModState.Fine) > 0 Then TypeList.Add("启用 " & Counter(McMod.McModState.Fine))
        If Counter(McMod.McModState.Disabled) > 0 Then TypeList.Add("禁用 " & Counter(McMod.McModState.Disabled))
        If Counter(McMod.McModState.Unavaliable) > 0 Then TypeList.Add("错误 " & Counter(McMod.McModState.Unavaliable))
        Return "Mod 列表（" & Join(TypeList, "，") & "）"
    End Function
    Private Function McModListItem(Entry As McMod) As MyListItem
        '图标
        Dim Logo As String
        Select Case Entry.State
            Case McMod.McModState.Fine
                Logo = "pack://application:,,,/images/Blocks/RedstoneLampOn.png"
            Case McMod.McModState.Disabled
                Logo = "pack://application:,,,/images/Blocks/RedstoneLampOff.png"
            Case Else '出错
                Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png"
        End Select
        '文本
        Dim Title As String
        If Entry.State = McMod.McModState.Disabled Then
            Title = GetFileNameWithoutExtentionFromPath(Entry.Path.Substring(0, Entry.Path.Count - ".disabled".Count)) & "（已禁用）"
        Else
            Title = GetFileNameWithoutExtentionFromPath(Entry.Path)
        End If
        Dim Desc As String
        If Entry.Version Is Nothing OrElse Entry.Description IsNot Nothing Then
            Desc = Entry.Name & If(Entry.Version Is Nothing, "", " (" & Entry.Version & ")") & " : " & If(Entry.Description, Entry.Path)
        ElseIf Entry.IsFileAvailable Then
            Desc = Entry.Path
        Else
            Desc = "存在错误 : " & Entry.Path
        End If
        'Desc = If(Entry.ModId, "无可用名称") & " (" & If(Entry.Version, "无可用版本") & ")"
        'If Entry.Dependencies.Count > 0 Then
        '    Dim DepList As New List(Of String)
        '    For Each Dep In Entry.Dependencies
        '        DepList.Add(Dep.Key & If(Dep.Value Is Nothing, "", "@" & Dep.Value))
        '    Next
        '    Desc += " : " & Join(DepList, ", ")
        'End If
        '实例化
        AniControlEnabled += 1
        Dim NewItem As New MyListItem With {
            .LogoClickable = True, .Logo = Logo, .SnapsToDevicePixels = True, .Title = Title, .Info = Desc, .Height = 42, .Tag = Entry,
            .Type = MyListItem.CheckType.CheckBox, .IsScaleAnimationEnabled = False,
            .PaddingRight = 73,
            .ContentHandler = AddressOf McModContent, .Checked = SelectedMods.Contains(Entry.RawFileName)
        }
        AniControlEnabled -= 1
        Return NewItem
    End Function
    Private Sub McModContent(sender As MyListItem, e As EventArgs)
        Dim ModEntity As McMod = sender.Tag
        '注册点击事件
        AddHandler sender.Changed, AddressOf CheckChanged
        AddHandler sender.LogoClick, AddressOf ItemLogo_Click
        '图标按钮
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete, .Tag = sender}
        BtnDel.ToolTip = "删除"
        ToolTipService.SetPlacement(BtnDel, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnDel, 30)
        ToolTipService.SetHorizontalOffset(BtnDel, 2)
        AddHandler BtnDel.Click, AddressOf Delete_Click
        Dim BtnOpen As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen, .Tag = sender}
        BtnOpen.ToolTip = "打开文件位置"
        ToolTipService.SetPlacement(BtnOpen, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnOpen, 30)
        ToolTipService.SetHorizontalOffset(BtnOpen, 2)
        AddHandler BtnOpen.Click, AddressOf Open_Click
        Dim BtnCont As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .Tag = sender}
        BtnCont.ToolTip = "详情"
        ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnCont, 30)
        ToolTipService.SetHorizontalOffset(BtnCont, 2)
        AddHandler BtnCont.Click, AddressOf Info_Click
        AddHandler sender.MouseRightButtonDown, AddressOf Info_Click
        sender.Buttons = {BtnCont, BtnOpen, BtnDel}
    End Sub

#End Region

#Region "管理"

    ''' <summary>
    ''' 打开 Mods 文件夹。
    ''' </summary>
    Private Sub BtnManageOpen_Click(sender As Object, e As EventArgs) Handles BtnManageOpen.Click, BtnHintOpen.Click
        Try
            Directory.CreateDirectory(PageVersionLeft.Version.PathIndie & "mods\")
            OpenExplorer("""" & PageVersionLeft.Version.PathIndie & "mods\""")
        Catch ex As Exception
            Log(ex, "打开 Mods 文件夹失败", LogLevel.Msgbox)
        End Try
    End Sub

#If DEBUG Then
    ''' <summary>
    ''' 检查 Mod。
    ''' </summary>
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            Dim Result = McModCheck(PageVersionLeft.Version, McModLoader.Output)
            If Result.Count > 0 Then
                MyMsgBox(Join(Result, vbCrLf & vbCrLf), "Mod 检查结果")
            Else
                Hint("Mod 检查完成，未发现任何问题！", HintType.Finish)
            End If
        Catch ex As Exception
            Log(ex, "进行 Mod 检查时出错", LogLevel.Feedback)
        End Try
    End Sub
#End If

    ''' <summary>
    ''' 全选。
    ''' </summary>
    Private Sub BtnManageSelectAll_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageSelectAll.Click
        ChangeAllSelected(True)
    End Sub

#End Region

#Region "选择"

    '选择的 Mod 的路径（不含 .disabled）
    Public SelectedMods As New List(Of String)

    '单项切换选择状态
    Public Sub CheckChanged(sender As MyListItem, e As RouteEventArgs)
        If AniControlEnabled <> 0 Then Return
        '更新选择了的内容
        Dim SelectedKey As String = CType(sender.Tag, McMod).RawFileName
        If sender.Checked Then
            If Not SelectedMods.Contains(SelectedKey) Then SelectedMods.Add(SelectedKey)
        Else
            SelectedMods.Remove(SelectedKey)
        End If
        '更新下边栏 UI
        RefreshBottomBar()
    End Sub

    '改变下边栏状态
    Private ShownCount As Integer = 0
    Private Sub RefreshBottomBar()
        '计数
        Dim NewCount As Integer = SelectedMods.Count
        Dim Selected = NewCount > 0
        If Selected Then LabSelect.Text = $"已选择 {NewCount} 个文件" '取消所有选择时不更新数字
        '按钮可用性
        If Selected Then
            Dim HasEnabled As Boolean = False
            Dim HasDisabled As Boolean = False
            For Each ModEntity In McModLoader.Output
                If SelectedMods.Contains(ModEntity.RawFileName) Then
                    If ModEntity.State = McMod.McModState.Fine Then
                        HasEnabled = True
                    ElseIf ModEntity.State = McMod.McModState.Disabled Then
                        HasDisabled = True
                    End If
                End If
            Next
            BtnSelectDisable.IsEnabled = HasEnabled
            BtnSelectEnable.IsEnabled = HasDisabled
        End If
        '更新显示状态
        CardSelect.IsHitTestVisible = Selected
        If AniControlEnabled = 0 Then
            If Selected Then
                '仅在数量增加时播放出现/跳跃动画
                If ShownCount >= NewCount Then
                    ShownCount = NewCount
                    Return
                Else
                    ShownCount = NewCount
                End If
                '出现/跳跃动画
                CardSelect.Visibility = Visibility.Visible
                AniStart({
                    AaOpacity(CardSelect, 1 - CardSelect.Opacity, 60),
                    AaTranslateY(CardSelect, -27 - TransSelect.Y, 120, Ease:=New AniEaseOutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, 3, 150, 120, Ease:=New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, -1, 90, 270, Ease:=New AniEaseInoutFluent(AniEasePower.Weak))
                }, "Mod Sidebar")
            Else
                '不重复播放隐藏动画
                If ShownCount = 0 Then Return
                ShownCount = 0
                '隐藏动画
                AniStart({
                    AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                    AaTranslateY(CardSelect, -10 - TransSelect.Y, 90, Ease:=New AniEaseInFluent(AniEasePower.Weak)),
                    AaCode(Sub() CardSelect.Visibility = Visibility.Collapsed, After:=True)
                }, "Mod Sidebar")
            End If
        Else
            AniStop("Mod Sidebar")
            ShownCount = NewCount
            If Selected Then
                CardSelect.Visibility = Visibility.Visible
                CardSelect.Opacity = 1
                TransSelect.Y = -25
            Else
                CardSelect.Visibility = Visibility.Collapsed
                CardSelect.Opacity = 0
                TransSelect.Y = -10
            End If
        End If
    End Sub

    '切换所有项的选择状态
    Private Sub ChangeAllSelected(Value As Boolean)
        AniControlEnabled += 1
        SelectedMods.Clear()
        If IsSearching Then
            '搜索中
            For Each Item As MyListItem In PanSearchList.Children
                Item.Checked = Value
                If Value Then SelectedMods.Add(CType(Item.Tag, McMod).RawFileName)
            Next
            If Not Value Then '只取消选择
                If PanItems IsNot Nothing Then
                    For Each Item As MyListItem In PanItems.Children
                        Item.Checked = Value
                    Next
                End If
            End If
        Else
            '非搜索中
            If PanItems IsNot Nothing Then
                For Each Item As MyListItem In PanItems.Children
                    Item.Checked = Value
                    If Value Then SelectedMods.Add(CType(Item.Tag, McMod).RawFileName)
                Next
            End If
        End If
        AniControlEnabled -= 1
        '更新下边栏 UI
        RefreshBottomBar()
    End Sub
    Private Sub Load_State(sender As Object, newState As MyLoadingState, oldState As MyLoadingState) Handles Load.StateChanged
        ChangeAllSelected(False)
    End Sub

#End Region

#Region "下边栏"

    '启用 / 禁用
    Private Sub BtnSelectEorD_Click(sender As MyIconTextButton, e As RouteEventArgs) Handles BtnSelectEnable.Click, BtnSelectDisable.Click
        Dim IsSuccessful As Boolean = True
        Dim IsDisable As Boolean = sender.Equals(BtnSelectDisable)
        For Each ModEntity In McModLoader.Output.Where(Function(m) SelectedMods.Contains(m.RawFileName)).ToList()
            Dim NewPath As String = Nothing
            If ModEntity.State = McMod.McModState.Fine And IsDisable Then
                '禁用
                NewPath = ModEntity.Path & ".disabled"
            ElseIf ModEntity.State = McMod.McModState.Disabled AndAlso Not IsDisable Then
                '启用
                NewPath = ModEntity.Path.Substring(0, ModEntity.Path.Count - ".disabled".Count)
            Else
                Continue For
            End If
            '重命名
            Try
                If File.Exists(NewPath) AndAlso Not File.Exists(ModEntity.Path) Then Continue For '因为未知原因 Mod 的状态已经切换完了
                File.Delete(NewPath)
                FileSystem.Rename(ModEntity.Path, NewPath)
            Catch ex As FileNotFoundException
                Log(ex, $"未找到需要重命名的 Mod（{If(ModEntity.Path, "null")}）", LogLevel.Feedback)
                RefreshList(True)
                Return
            Catch ex As Exception
                Log(ex, $"批量重命名 Mod 失败（{If(ModEntity.Path, "null")}）")
                IsSuccessful = False
            End Try
            '更改 Loader 和 UI 中的列表
            Dim NewModEntity As New McMod(NewPath)
            Dim IndexOfLoader As Integer = McModLoader.Output.IndexOf(ModEntity)
            McModLoader.Output.RemoveAt(IndexOfLoader)
            McModLoader.Output.Insert(IndexOfLoader, NewModEntity)
            Dim Parent As StackPanel = If(IsSearching, PanSearchList, PanItems)
            Dim IndexOfUi As Integer = Parent.Children.IndexOf(Parent.Children.OfType(Of MyListItem).First(Function(i) CType(i.Tag, McMod) Is ModEntity))
            Parent.Children.RemoveAt(IndexOfUi)
            Parent.Children.Insert(IndexOfUi, McModListItem(NewModEntity))
        Next
        If Not IsSearching Then
            '改变禁用数量的显示
            CType(PanItems.Parent, MyCard).Title = McModGetTitle(McModLoader.Output)
            '更新加载器状态
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.UpdateOnly)
        End If
        If Not IsSuccessful Then
            Hint("由于文件被占用，部分 Mod 的状态切换失败，请尝试关闭正在运行的游戏后再试！", HintType.Critical)
            RefreshList(True)
        Else
            ChangeAllSelected(False)
        End If
    End Sub

    '删除
    Private Sub BtnSelectDelete_Click() Handles BtnSelectDelete.Click
        Dim IsSuccessful As Boolean = True
        Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
        Dim DeleteList As List(Of McMod) = McModLoader.Output.Where(Function(m) SelectedMods.Contains(m.RawFileName)).ToList()
        For Each ModEntity In DeleteList
            '删除
            Try
                If IsShiftPressed Then
                    File.Delete(ModEntity.Path)
                Else
                    My.Computer.FileSystem.DeleteFile(ModEntity.Path, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                End If
            Catch ex As OperationCanceledException
                Log(ex, "批量删除 Mod 被主动取消")
                RefreshList(True)
                Return
            Catch ex As Exception
                Log(ex, $"批量删除 Mod 失败（{ModEntity.Path}）", LogLevel.Msgbox)
                IsSuccessful = False
            End Try
            '更改 Loader 和 UI 中的列表
            McModLoader.Output.Remove(ModEntity)
            Dim Parent As StackPanel = If(IsSearching, PanSearchList, PanItems)
            Dim IndexOfUi As Integer = Parent.Children.IndexOf(Parent.Children.OfType(Of MyListItem).First(Function(i) CType(i.Tag, McMod) Is ModEntity))
            Parent.Children.RemoveAt(IndexOfUi)
        Next
        If Not IsSearching Then
            '改变禁用数量的显示
            CType(PanItems.Parent, MyCard).Title = McModGetTitle(McModLoader.Output)
            '更新加载器状态
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.UpdateOnly)
        End If
        If Not IsSuccessful Then
            Hint("由于文件被占用，部分 Mod 删除失败，请尝试关闭正在运行的游戏后再试！", HintType.Critical)
            RefreshList(True)
        ElseIf If(IsSearching, PanSearchList, PanItems).Children.Count = 0 Then
            RefreshList(True)
        Else
            ChangeAllSelected(False)
        End If
        If IsSuccessful Then
            If IsShiftPressed Then
                Hint($"已彻底删除 {DeleteList.Count} 个文件！", HintType.Finish)
            Else
                Hint($"已将 {DeleteList.Count} 个文件删除到回收站！", HintType.Finish)
            End If
        End If
    End Sub

    '取消选择
    Private Sub BtnSelectCancel_Click() Handles BtnSelectCancel.Click
        ChangeAllSelected(False)
    End Sub

#End Region

#Region "单个 Mod 项"

    '点击 Logo：切换可用状态 / 显示错误原因
    Public Sub ItemLogo_Click(sender As MyListItem, e As EventArgs)
        Try

            Dim ModEntity As McMod = sender.Tag
            Dim NewPath As String = Nothing
            Select Case ModEntity.State
                Case McMod.McModState.Fine
                    '前置检测警告
                    If ModEntity.IsPresetMod Then
                        If MyMsgBox("该 Mod 可能为其他 Mod 的前置，如果禁用可能导致其他 Mod 无法使用。" & vbCrLf & "你确定要继续禁用吗？", "警告", "禁用", "取消") = 2 Then Exit Sub
                    End If
                    NewPath = ModEntity.Path & ".disabled"
                Case McMod.McModState.Disabled
                    NewPath = ModEntity.Path.Substring(0, ModEntity.Path.Count - ".disabled".Count)
                Case McMod.McModState.Unavaliable
                    MyMsgBox("无法读取此 Mod 的信息。" & vbCrLf & vbCrLf & "详细的错误信息：" & GetExceptionDetail(ModEntity.FileUnavailableReason), "Mod 读取失败")
                    Exit Sub
            End Select
            '重命名
            Dim NewModEntity As New McMod(NewPath)
            Try
                File.Delete(NewPath)
                FileSystem.Rename(ModEntity.Path, NewPath)
            Catch ex As FileNotFoundException
                Log(ex, "未找到理应存在的 Mod 文件（" & ModEntity.Path & "）")
                FileSystem.Rename(NewPath, ModEntity.Path)
                NewModEntity = New McMod(ModEntity.Path)
            End Try
            '更改 Loader 中的列表
            Dim IndexOfLoader As Integer = McModLoader.Output.IndexOf(ModEntity)
            McModLoader.Output.RemoveAt(IndexOfLoader)
            McModLoader.Output.Insert(IndexOfLoader, NewModEntity)
            '更改 UI 中的列表
            Dim Parent As StackPanel = sender.Parent
            Dim IndexOfUi As Integer = Parent.Children.IndexOf(sender)
            Parent.Children.RemoveAt(IndexOfUi)
            Parent.Children.Insert(IndexOfUi, McModListItem(NewModEntity))
            '仅在非搜索页面才执行，以确保搜索页内外显示一致
            If Not IsSearching Then
                '改变禁用数量的显示
                CType(Parent.Parent, MyCard).Title = McModGetTitle(McModLoader.Output)
                '更新加载器状态
                LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.UpdateOnly)
            End If
            RefreshBottomBar()

        Catch ex As Exception
            Log(ex, "单个状态改变中重命名 Mod 失败")
            Hint("切换 Mod 状态失败，请尝试关闭正在运行的游戏后再试！")
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.UpdateOnly)
        End Try
    End Sub
    '删除
    Public Sub Delete_Click(sender As MyIconButton, e As EventArgs)
        Try

            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim ListItem As MyListItem = sender.Tag
            Dim ModEntity As McMod = ListItem.Tag
            '前置检测警告
            If ModEntity.IsPresetMod Then
                If MyMsgBox("该 Mod 可能为其他 Mod 的前置，如果删除可能导致其他 Mod 无法使用。" & vbCrLf & "你确定要继续删除吗？", "警告", "删除", "取消", IsWarn:=True) = 2 Then Exit Sub
            End If
            '删除
            If File.Exists(ModEntity.Path) Then
                If IsShiftPressed Then
                    File.Delete(ModEntity.Path)
                Else
                    My.Computer.FileSystem.DeleteFile(ModEntity.Path, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                End If
            Else
                '在不明原因下删除的文件可能还在列表里，如果玩家又点了一次就会出错，总之加个判断也不碍事
                Log("[System] 需要删除的 Mod 文件不存在（" & ModEntity.Path & "）", LogLevel.Hint)
                Exit Sub
            End If
            '更改 Loader 中的列表
            McModLoader.Output.Remove(ModEntity)
            '更改 UI 中的列表
            Dim Parent As StackPanel = ListItem.Parent
            Parent.Children.Remove(ListItem)
            If Parent.Children.Count = 0 Then
                RefreshList(True)
            Else
                CType(Parent.Parent, MyCard).Title = McModGetTitle(McModLoader.Output)
            End If
            '显示提示
            If IsShiftPressed Then
                Hint("已删除 " & ModEntity.FileName & "！", HintType.Finish)
            Else
                Hint("已将 " & ModEntity.FileName & " 删除到回收站！", HintType.Finish)
            End If
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.UpdateOnly)

        Catch ex As OperationCanceledException
            Log(ex, "删除 Mod 被主动取消")
        Catch ex As Exception
            Log(ex, "删除 Mod 失败", LogLevel.Feedback)
        End Try
    End Sub
    '详情
    Public Sub Info_Click(sender As Object, e As EventArgs)
        Try

            Dim ListItem As MyListItem
            ListItem = If(TypeOf sender Is MyIconButton, sender.Tag, sender)
            Dim ModEntity As McMod = ListItem.Tag
            '加载失败信息
            If ModEntity.State = McMod.McModState.Unavaliable Then
                MyMsgBox("无法读取此 Mod 的信息。" & vbCrLf & vbCrLf & "详细的错误信息：" & GetExceptionDetail(ModEntity.FileUnavailableReason), "Mod 读取失败")
                Return
            End If
            '获取信息
            Dim ContentLines As New List(Of String)
            If ModEntity.Description IsNot Nothing Then ContentLines.Add(ModEntity.Description & vbCrLf)
            If ModEntity.Authors IsNot Nothing Then ContentLines.Add("作者：" & ModEntity.Authors)
            ContentLines.Add("文件：" & ModEntity.FileName & "（" & GetString(New FileInfo(ModEntity.Path).Length) & "）")
            If ModEntity.Version IsNot Nothing Then ContentLines.Add("版本：" & ModEntity.Version)
            Dim DebugInfo As New List(Of String)
            If ModEntity.ModId IsNot Nothing Then
                DebugInfo.Add("Mod ID：" & ModEntity.ModId)
            End If
            If ModEntity.Dependencies.Count > 0 Then
                DebugInfo.Add("依赖于：")
                For Each Dep In ModEntity.Dependencies
                    DebugInfo.Add(" - " & Dep.Key & If(Dep.Value Is Nothing, "", "，版本：" & Dep.Value))
                Next
            End If
            If DebugInfo.Count > 0 Then
                ContentLines.Add("")
                ContentLines.AddRange(DebugInfo)
            End If
            '获取用于搜索的 Mod 名称
            Dim ModOriginalName As String = ModEntity.Name.Replace(" ", "+")
            Dim ModSearchName As String = ModOriginalName.Substring(0, 1)
            For i = 1 To ModOriginalName.Count - 1
                Dim IsLastLower As Boolean = ModOriginalName(i - 1).ToString.ToLower.Equals(ModOriginalName(i - 1).ToString)
                Dim IsCurrentLower As Boolean = ModOriginalName(i).ToString.ToLower.Equals(ModOriginalName(i).ToString)
                If IsLastLower AndAlso Not IsCurrentLower Then
                    '上一个字母为小写，这一个字母为大写
                    ModSearchName += "+"
                End If
                ModSearchName += ModOriginalName(i)
            Next
            ModSearchName = ModSearchName.Replace("++", "+").Replace("pti+Fine", "ptiFine")
            '显示
            If ModEntity.Url Is Nothing Then
                If MyMsgBox(Join(ContentLines, vbCrLf), ModEntity.Name, "百科搜索", "返回") = 1 Then
                    OpenWebsite("https://www.mcmod.cn/s?key=" & ModSearchName & "&site=all&filter=0")
                End If
            Else
                Select Case MyMsgBox(Join(ContentLines, vbCrLf), ModEntity.Name, "打开官网", "百科搜索", "返回")
                    Case 1
                        OpenWebsite(ModEntity.Url)
                    Case 2
                        OpenWebsite("https://www.mcmod.cn/s?key=" & ModSearchName & "&site=all&filter=0")
                End Select
            End If

        Catch ex As Exception
            Log(ex, "获取 Mod 详情失败", LogLevel.Feedback)
        End Try
    End Sub
    '打开文件所在的位置
    Public Sub Open_Click(sender As MyIconButton, e As EventArgs)
        Try

            Dim ListItem As MyListItem = sender.Tag
            Dim ModEntity As McMod = ListItem.Tag
            OpenExplorer("/select,""" & ModEntity.Path & """")

        Catch ex As Exception
            Log(ex, "打开 Mod 文件位置失败", LogLevel.Feedback)
        End Try
    End Sub

#End Region

    Public ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(SearchBox.Text)
        End Get
    End Property
    Public Sub SearchRun() Handles SearchBox.TextChanged
        ChangeAllSelected(False)
        If IsSearching Then
            '构造请求
            Dim QueryList As New List(Of SearchEntry(Of McMod))
            For Each Entry As McMod In McModLoader.Output
                QueryList.Add(New SearchEntry(Of McMod) With {
                    .Item = Entry,
                    .SearchSource = New List(Of KeyValuePair(Of String, Double)) From {
                        New KeyValuePair(Of String, Double)(Entry.Name, 1),
                        New KeyValuePair(Of String, Double)(Entry.FileName, 1),
                        New KeyValuePair(Of String, Double)(If(Entry.Description, ""), 0.5)
                    }
                })
            Next
            '进行搜索，构造列表
            Dim SearchResult = Search(QueryList, SearchBox.Text, MaxBlurCount:=5, MinBlurSimilarity:=0.35)
            PanSearchList.Children.Clear()
            If SearchResult.Count = 0 Then
                PanSearch.Title = "无搜索结果"
                PanSearchList.Visibility = Visibility.Collapsed
            Else
                PanSearch.Title = "搜索结果"
                For Each Result In SearchResult
                    Dim Item = McModListItem(Result.Item)
                    If ModeDebug Then Item.Info = If(Result.AbsoluteRight, "完全匹配，", "") & "相似度：" & Math.Round(Result.Similarity, 3) & "，" & Item.Info
                    PanSearchList.Children.Add(Item)
                Next
                PanSearchList.Visibility = Visibility.Visible
            End If
            '显示
            AniStart({
                     AaOpacity(PanList, -PanList.Opacity, 100),
                     AaCode(Sub()
                                PanList.Visibility = Visibility.Collapsed
                                PanSearch.Visibility = Visibility.Visible
                                PanSearch.TriggerForceResize()
                            End Sub,, True),
                     AaOpacity(PanSearch, 1 - PanSearch.Opacity, 200, 60)
                }, "FrmVersionMod Search Switch", True)
        Else
            '隐藏
            LoaderFolderRun(McModLoader, PageVersionLeft.Version.PathIndie & "mods\", LoaderFolderRunType.RunOnUpdated)
            AniStart({
                     AaOpacity(PanSearch, -PanSearch.Opacity, 100),
                     AaCode(Sub()
                                PanSearch.Height = 0
                                PanSearch.Visibility = Visibility.Collapsed
                                PanList.Visibility = Visibility.Visible
                            End Sub,, True),
                     AaOpacity(PanList, 1 - PanList.Opacity, 150, 30)
                }, "FrmVersionMod Search Switch", True)
        End If
    End Sub

End Class
