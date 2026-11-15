; *** Inno Setup version 6.5.0+ Chinese Simplified messages ***
; Name: GoneTone, gonetone@reh.tw
;
; Based on translations by: Anbang LI, anbangli@outlook.com
; Based on translations by: Enfong Tsao, nelson22768384@gmail.com
; Based on 5.5.3+ translations by Samuel Lee, Email: 751555749@qq.com
; Translation based on network resource
;
; Note: When translating this text, do not add periods (.) to the end of
; messages that didn't have them already, because on those messages Inno
; Setup adds the periods automatically (appending a period would result in
; two periods being displayed).
;
; Submit webpage: https://jrsoftware.org/files/istrans/

[LangOptions]
; The following three entries are very important. Be sure to read and
; understand the '[LangOptions] section' topic in the help file.
LanguageName=简体中文
; About LanguageID, to reference link:
; https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c
LanguageID=$0804
; About CodePage, to reference link:
; https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers
LanguageCodePage=936
; If the language you are translating to requires special font faces or
; sizes, uncomment any of the following entries and change them accordingly.
DialogFontName=Microsoft YaHei UI
;DialogFontSize=9
;DialogFontBaseScaleWidth=7
;DialogFontBaseScaleHeight=15
WelcomeFontName=Microsoft YaHei UI
;WelcomeFontSize=14

[Messages]

; *** Application titles
SetupAppTitle=安装程式
SetupWindowTitle=%1 安装程式
UninstallAppTitle=解除安装
UninstallAppFullTitle=解除安装 %1

; *** Misc. common
InformationTitle=资讯
ConfirmTitle=确认
ErrorTitle=错误

; *** SetupLdr messages
SetupLdrStartupMessage=这将会安装 %1。您想要继续吗？
LdrCannotCreateTemp=无法建立暂存档案。安装程式将会结束
LdrCannotExecTemp=无法执行暂存档案。安装程式将会结束
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1。%n%n错误 %2：%3
SetupFileMissing=安装资料夹中遗失档案 %1。请修正此问题或重新取得此软体。
SetupFileCorrupt=安装档案已经损毁。请重新取得此软体。
SetupFileCorruptOrWrongVer=安装档案已经损毁，或与安装程式的版本不符。请修正此问题或重新取得此软体。
InvalidParameter=某个无效的参数已传递至命令列：%n%n%1
SetupAlreadyRunning=安装程式已经在执行。
WindowsVersionNotSupported=这个程式并不支援目前在电脑所执行的 Windows 版本。
WindowsServicePackRequired=这个程式需要 %1 Service Pack %2 或更新。
NotOnThisPlatform=这个程式无法在 %1 执行。
OnlyOnThisPlatform=这个程式必须在 %1 执行。
OnlyOnTheseArchitectures=这个程式只能在专门为以下处理器架构而设计的 Windows 上安装：%n%n%1
WinVersionTooLowError=这个程式必须在 %1 版本 %2 或以上的系统执行。
WinVersionTooHighError=这个程式无法安装在 %1 版本 %2 或以上的系统。
AdminPrivilegesRequired=您必须登入成系统管理员以安装这个程式。
PowerUserPrivilegesRequired=您必须登入成系统管理员或 Power Users 群组的成员以安装这个程式。
SetupAppRunningError=安装程式侦测到 %1 正在执行。%n%n请立即关闭它的所有执行个体，然後按 「确定」 继续，或按 「取消」 离开。
UninstallAppRunningError=解除安装程式侦测到 %1 正在执行。%n%n请立即关闭它的所有执行个体，然後按 「确定」 继续，或按 「取消」 离开。

; *** Startup questions
PrivilegesRequiredOverrideTitle=选择安装程式安装模式
PrivilegesRequiredOverrideInstruction=选择安装模式
PrivilegesRequiredOverrideText1=可以为所有使用者安装 %1 (需要系统管理员权限)，或是仅为您安装。
PrivilegesRequiredOverrideText2=可以仅为您安装 %1，或是为所有使用者安装 (需要系统管理员权限)。
PrivilegesRequiredOverrideAllUsers=为所有使用者安装 (&A)
PrivilegesRequiredOverrideAllUsersRecommended=为所有使用者安装 (建议选项) (&A)
PrivilegesRequiredOverrideCurrentUser=仅为我安装 (&M)
PrivilegesRequiredOverrideCurrentUserRecommended=仅为我安装 (建议选项) (&M)

; *** Misc. errors
ErrorCreatingDir=安装程式无法建立资料夹「%1」
ErrorTooManyFilesInDir=无法在资料夹「%1」内建立档案，因为资料夹内有太多的档案。

; *** Setup common messages
ExitSetupTitle=结束安装程式
ExitSetupMessage=安装尚未完成。如果您现在结束安装程式，这个程式将不会被安装。%n%n您可以稍後再执行安装程式以完成安装。%n%n您现在要结束安装程式吗？
AboutSetupMenuItem=关於安装程式 (&A)...
AboutSetupTitle=关於安装程式
AboutSetupMessage=%1 版本 %2%n%3%n%n%1 网址：%n%4
AboutSetupNote=
TranslatorNote=

; *** Buttons
ButtonBack=< 上一步 (&B)
ButtonNext=下一步 (&N) >
ButtonInstall=安装 (&I)
ButtonOK=确定
ButtonCancel=取消
ButtonYes=是 (&Y)
ButtonYesToAll=全部皆是 (&A)
ButtonNo=否 (&N)
ButtonNoToAll=全部皆否 (&O)
ButtonFinish=完成 (&F)
ButtonBrowse=浏览 (&B)...
ButtonWizardBrowse=浏览 (&R)...
ButtonNewFolder=建立新资料夹 (&M)

; *** "Select Language" dialog messages
SelectLanguageTitle=选择安装语言
SelectLanguageLabel=选择在安装过程中使用的语言：

; *** Common wizard text
ClickNext=按 「下一步」 继续，或按 「取消」 结束安装程式。
BeveledLabel=
BrowseDialogTitle=浏览资料夹
BrowseDialogLabel=在下面的资料夹清单中选择一个资料夹，然後按 「确定」。
NewFolderName=新资料夹

; *** "Welcome" wizard page
WelcomeLabel1=欢迎使用 [name] 安装程式
WelcomeLabel2=这个安装程式将会安装 [name/ver] 到您的电脑。%n%n建议您在继续之前关闭所有其他的应用程式。

; *** "Password" wizard page
WizardPassword=密码
PasswordLabel1=这个安装程式具有密码保护。
PasswordLabel3=请输入密码，然後按 「下一步」 继续。密码是区分大小写的。
PasswordEditLabel=密码 (&P)：
IncorrectPassword=您输入的密码不正确，请重新输入。

; *** "License Agreement" wizard page
WizardLicense=授权合约
LicenseLabel=在继续安装之前请阅读以下重要资讯。
LicenseLabel3=请阅读以下授权合约，您必须接受合约的各项条款才能继续安装。
LicenseAccepted=我同意 (&A)
LicenseNotAccepted=我不同意 (&D)

; *** "Information" wizard pages
WizardInfoBefore=资讯
InfoBeforeLabel=在继续安装之前请阅读以下重要资讯。
InfoBeforeClickLabel=当您准备好继续安装，请按 「下一步」。
WizardInfoAfter=资讯
InfoAfterLabel=在继续安装之前请阅读以下重要资讯。
InfoAfterClickLabel=当您准备好继续安装，请按 「下一步」。

; *** "User Information" wizard page
WizardUserInfo=使用者资讯
UserInfoDesc=请输入您的资料。
UserInfoName=使用者名称 (&U)：
UserInfoOrg=组织 (&O)：
UserInfoSerial=序号 (&S)：
UserInfoNameRequired=您必须输入您的名称。

; *** "Select Destination Location" wizard page
WizardSelectDir=选择目的资料夹
SelectDirDesc=选择安装程式安装 [name] 的位置。
SelectDirLabel3=安装程式将会把 [name] 安装到下面的资料夹。
SelectDirBrowseLabel=按 「下一步」 继续，如果您想选择另一个资料夹，请按 「浏览」。
DiskSpaceGBLabel=最少需要 [gb] GB 可用磁碟空间。
DiskSpaceMBLabel=最少需要 [mb] MB 可用磁碟空间。
CannotInstallToNetworkDrive=安装程式无法安装於网路磁碟机。
CannotInstallToUNCPath=安装程式无法安装於 UNC 路径。
InvalidPath=您必须输入完整的路径名称及磁碟机代码，例如：%n%nC:\App%n%n或是 UNC 路径格式：%n%n\\伺服器\共用资料夹
InvalidDrive=您选取的磁碟机或 UNC 共用不存在或无法存取，请另外选择。
DiskSpaceWarningTitle=磁碟空间不足
DiskSpaceWarning=安装程式需要至少 %1 KB 的可用空间，您所选取的磁碟只有 %2 KB 可用空间。%n%n您仍要继续安装吗？
DirNameTooLong=资料夹名称或路径太长。
InvalidDirName=资料夹名称不正确。
BadDirName32=资料夹名称不得包含以下字元：%n%n%1
DirExistsTitle=资料夹已经存在
DirExists=资料夹：%n%n%1%n%n已经存在。仍要安装到该资料夹吗？
DirDoesntExistTitle=资料夹不存在
DirDoesntExist=资料夹：%n%n%1%n%n不存在。要建立该资料夹吗？

; *** "Select Components" wizard page
WizardSelectComponents=选择元件
SelectComponentsDesc=选择将会被安装的元件。
SelectComponentsLabel2=选择您想要安装的元件；清除您不想安装的元件。当您准备好继续安装，请按 「下一步」。
FullInstallation=完整安装
; if possible don't translate 'Compact' as 'Minimal' (I mean 'Minimal' in your language)
CompactInstallation=精简安装
CustomInstallation=自订安装
NoUninstallWarningTitle=元件已存在
NoUninstallWarning=安装程式侦测到以下元件已经安装在您的电脑上：%n%n%1%n%n取消选择这些元件将不会移除它们。%n%n您仍然要继续吗？
ComponentSize1=%1 KB
ComponentSize2=%1 MB
ComponentsDiskSpaceGBLabel=目前的选择需要至少 [gb] GB 磁碟空间。
ComponentsDiskSpaceMBLabel=目前的选择需要至少 [mb] MB 磁碟空间。

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=选择附加工作
SelectTasksDesc=选择要执行的附加工作。
SelectTasksLabel2=选择安装程式在安装 [name] 时要执行的附加工作，然後按 「下一步」。

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=选择「开始」功能表的资料夹
SelectStartMenuFolderDesc=选择安装程式建立程式的捷径的位置。
SelectStartMenuFolderLabel3=安装程式将会把程式的捷径建立在下面的「开始」功能表资料夹。
SelectStartMenuFolderBrowseLabel=按 「下一步」 继续，如果您想选择另一个资料夹，请按 「浏览」。
MustEnterGroupName=您必须输入一个资料夹的名称。
GroupNameTooLong=资料夹名称或路径太长。
InvalidGroupName=资料夹名称不正确。
BadGroupName=资料夹名称不得包含下列字元：%n%n%1
NoProgramGroupCheck2=不要在「开始」功能表中建立资料夹 (&D)

; *** "Ready to Install" wizard page
WizardReady=准备安装
ReadyLabel1=安装程式将开始安装 [name] 到您的电脑中。
ReadyLabel2a=按下 「安装」 继续安装，或按 「上一步」 重新检视或设定各选项的内容。
ReadyLabel2b=按下 「安装」 继续安装。
ReadyMemoUserInfo=使用者资讯：
ReadyMemoDir=目的资料夹：
ReadyMemoType=安装型态：
ReadyMemoComponents=选择的元件：
ReadyMemoGroup=「开始」功能表资料夹：
ReadyMemoTasks=附加工作：

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel2=正在下载档案...
ButtonStopDownload=停止下载 (&S)
StopDownload=您确定要停止下载吗？
ErrorDownloadAborted=已停止下载。
ErrorDownloadFailed=下载失败：%1 %2。
ErrorDownloadSizeFailed=取得档案大小失败：%1 %2。
ErrorProgress=进度无效：%1 / %2。
ErrorFileSize=档案大小无效：必须为 %1，收到 %2。

; *** TExtractionWizardPage wizard page and ExtractArchive
ExtractingLabel=正在解压缩档案...
ButtonStopExtraction=停止解压缩 (&S)
StopExtraction=您确定要停止解压缩吗？
ErrorExtractionAborted=解压缩已中止。
ErrorExtractionFailed=解压缩失败：%1

; *** Archive extraction failure details
ArchiveIncorrectPassword=压缩档密码不正确。
ArchiveIsCorrupted=压缩档已损毁。
ArchiveUnsupportedFormat=不支援的压缩档格式。

; *** "Preparing to Install" wizard page
WizardPreparing=准备安装程式
PreparingDesc=安装程式准备将 [name] 安装到您的电脑上。
PreviousInstallNotCompleted=先前的安装／解除安装尚未完成，您必须重新启动电脑以完成该安装。%n%n在重新启动电脑之後，请再次执行安装程式以完成 [name] 的安装。
CannotContinue=安装程式无法继续。请按 「取消」 离开。
ApplicationsFound=下面的应用程式正在使用安装程式所需要更新的档案。建议您允许安装程式自动关闭这些应用程式。
ApplicationsFound2=下面的应用程式正在使用安装程式所需要更新的档案。建议您允许安装程式自动关闭这些应用程式。当安装过程结束後，安装程式将会尝试重新启动这些应用程式。
CloseApplications=自动关闭应用程式 (&A)
DontCloseApplications=不要关闭应用程式 (&D)
ErrorCloseApplications=安装程式无法自动关闭所有应用程式。建议您在继续前先关闭所有正在使用安装程式所需更新档案的应用程式。
PrepareToInstallNeedsRestart=安装程式必须重新启动您的电脑。重新启动後，请再次执行安装程式以完成 [name] 的安装。%n%n您想要现在重新启动电脑吗？

; *** "Installing" wizard page
WizardInstalling=正在安装
InstallingLabel=请稍候，安装程式正在将 [name] 安装到您的电脑上。

; *** "Setup Completed" wizard page
FinishedHeadingLabel=正在完成 [name] 安装程式
FinishedLabelNoIcons=安装程式已经将 [name] 安装在您的电脑上。
FinishedLabel=安装程式已经将 [name] 安装在您的电脑中，您可以选择程式的捷径来执行该应用程式。
ClickFinish=按 「完成」 以结束安装程式。
FinishedRestartLabel=要完成 [name] 的安装，安装程式必须重新启动您的电脑。您想要现在重新启动电脑吗？
FinishedRestartMessage=要完成 [name] 的安装，安装程式必须重新启动您的电脑。%n%n您想要现在重新启动电脑吗？
ShowReadmeCheck=是，我要阅读读我档案
YesRadio=是，立即重新启动电脑 (&Y)
NoRadio=否，我稍後重新启动电脑 (&N)
; used for example as 'Run MyProg.exe'
RunEntryExec=执行 %1
; used for example as 'View Readme.txt'
RunEntryShellExec=检视 %1

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=安装程式需要下一张磁片
SelectDiskLabel2=请插入磁片 %1，然後按 「确定」。%n%n如果档案不在以下所显示的资料夹之中，请输入正确的路径或按 「浏览」 选取。
PathLabel=路径 (&P)：
FileNotInDir2=档案「%1」无法在「%2」找到。请插入正确的磁片或选择其他的资料夹。
SelectDirectoryLabel=请指定下一张磁片的位置。

; *** Installation phase messages
SetupAborted=安装没有完成。%n%n请更正问题後再次执行安装程式。
AbortRetryIgnoreSelectAction=选择操作
AbortRetryIgnoreRetry=重试 (&T)
AbortRetryIgnoreIgnore=略过错误并继续 (&I)
AbortRetryIgnoreCancel=取消安装
RetryCancelSelectAction=选择操作
RetryCancelRetry=重试 (&T)
RetryCancelCancel=取消

; *** Installation status messages
StatusClosingApplications=正在关闭应用程式...
StatusCreateDirs=正在建立资料夹...
StatusExtractFiles=正在解压缩档案...
StatusDownloadFiles=正在下载档案...
StatusCreateIcons=正在建立捷径...
StatusCreateIniEntries=正在建立 INI 项目...
StatusCreateRegistryEntries=正在建立登录项目...
StatusRegisterFiles=正在注册档案...
StatusSavingUninstall=正在储存解除安装资讯...
StatusRunProgram=正在完成安装...
StatusRestartingApplications=正在重新启动应用程式...
StatusRollback=正在复原变更...

; *** Misc. errors
ErrorInternal2=内部错误：%1。
ErrorFunctionFailedNoCode=%1 失败。
ErrorFunctionFailed=%1 失败；代码 %2。
ErrorFunctionFailedWithMessage=%1 失败；代码 %2。%n%3
ErrorExecutingProgram=无法执行档案：%n%1

; *** Registry errors
ErrorRegOpenKey=无法开启登录机码：%n%1\%2
ErrorRegCreateKey=无法建立登录机码：%n%1\%2
ErrorRegWriteKey=无法变更登录机码：%n%1\%2

; *** INI errors
ErrorIniEntry=在档案「%1」建立 INI 项目错误。

; *** File copying errors
FileAbortRetryIgnoreSkipNotRecommended=略过这个档案 (不建议) (&S)
FileAbortRetryIgnoreIgnoreNotRecommended=略过错误并继续 (不建议) (&I)
SourceDoesntExist=来源档案「%1」不存在。
SourceIsCorrupted=来源档案已经损毁。
SourceVerificationFailed=来源档案验证失败：%1
VerificationSignatureDoesntExist=签名档「%1」不存在。
VerificationSignatureInvalid=签名档「%1」无效。
VerificationKeyNotFound=签名档「%1」使用了未知金钥。
VerificationFileNameIncorrect=档案名称不正确。
VerificationFileTagIncorrect=档案标签不正确。
VerificationFileSizeIncorrect=档案大小不正确。
VerificationFileHashIncorrect=档案杂凑值不正确。
ExistingFileReadOnly2=无法取代现有档案，因为档案已标示为唯读。
ExistingFileReadOnlyRetry=移除唯读属性并重试 (&R)
ExistingFileReadOnlyKeepExisting=保留现有档案 (&K)
ErrorReadingExistingDest=读取现有档案时发生错误：
FileExistsSelectAction=选择操作
FileExists2=档案已存在。
FileExistsOverwriteExisting=覆写现有档案 (&O)
FileExistsKeepExisting=保留现有档案 (&K)
FileExistsOverwriteOrKeepAll=对下次冲突执行相同操作 (&D)
ExistingFileNewerSelectAction=选择操作
ExistingFileNewer2=现有档案比安装程式尝试安装的档案还新。
ExistingFileNewerOverwriteExisting=覆写现有档案 (&O)
ExistingFileNewerKeepExisting=保留现有档案 (&K) (建议选项)
ExistingFileNewerOverwriteOrKeepAll=对下次冲突执行相同操作 (&D)
ErrorChangingAttr=在变更现有档案的属性时发生错误：
ErrorCreatingTemp=在目的资料夹中建立档案时发生错误：
ErrorReadingSource=读取来源档案时发生错误：
ErrorCopying=复制档案时发生错误：
ErrorDownloading=下载档案时发生错误：
ErrorExtracting=解压缩压缩档时发生错误：
ErrorReplacingExistingFile=取代现有档案时发生错误：
ErrorRestartReplace=重新启动电脑後取代档案失败：
ErrorRenamingTemp=在目的资料夹变更档案名称时发生错误：
ErrorRegisterServer=无法注册 DLL/OCX 档案：%1
ErrorRegSvr32Failed=RegSvr32 失败；结束代码 %1。
ErrorRegisterTypeLib=无法注册类型库：%1

; *** Uninstall display name markings
; used for example as 'My Program (32-bit)'
UninstallDisplayNameMark=%1 (%2)
; used for example as 'My Program (32-bit, All users)'
UninstallDisplayNameMarks=%1 (%2, %3)
UninstallDisplayNameMark32Bit=32 位元
UninstallDisplayNameMark64Bit=64 位元
UninstallDisplayNameMarkAllUsers=所有使用者
UninstallDisplayNameMarkCurrentUser=目前使用者

; *** Post-installation errors
ErrorOpeningReadme=开启读我档案时发生错误。
ErrorRestartingComputer=安装程式无法重新启动电脑，请自行重新启动。

; *** Uninstaller messages
UninstallNotFound=档案「%1」不存在，无法解除安装。
UninstallOpenError=无法开启档案「%1」，无法解除安装
UninstallUnsupportedVer=这个版本的解除安装程式无法辨识解除安装记录档「%1」之格式，无法解除安装。
UninstallUnknownEntry=解除安装记录档中发现未知的记录 (%1)。
ConfirmUninstall=您确定要完全移除 %1 及其所有元件吗？
UninstallOnlyOnWin64=这个程式只能在 64 位元的 Windows 上解除安装。
OnlyAdminCanUninstall=这个程式要具备系统管理员权限的使用者方可解除安装。
UninstallStatusLabel=正在从您的电脑移除 %1 中，请稍候。
UninstalledAll=%1 已经成功从您的电脑中移除。
UninstalledMost=%1 解除安装完成。%n%n某些项目无法移除，您可以自行移除这些项目。
UninstalledAndNeedsRestart=要完成 %1 的解除安装，您必须重新启动电脑。%n%n您想要现在重新启动电脑吗？
UninstallDataCorrupted=档案「%1」已经损毁，无法解除安装。

; *** Uninstallation phase messages
ConfirmDeleteSharedFileTitle=移除共用档案
ConfirmDeleteSharedFile2=系统显示下列共用档案已不再被任何程式所使用，您要移除此共用档案吗？%n%n倘若您移除了以上档案但仍有程式需要使用它，可能造成这些程式无法正常执行，因此您若无法确定请选择 「否」。保留此档案在您的系统中不会造成任何损害。
SharedFileNameLabel=档案名称：
SharedFileLocationLabel=位置：
WizardUninstalling=解除安装状态
StatusUninstalling=正在解除安装 %1...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=正在安装 %1。
ShutdownBlockReasonUninstallingApp=正在解除安装 %1。

; The custom messages below aren't used by Setup itself, but if you make
; use of them in your scripts, you'll want to translate them.

[CustomMessages]

NameAndVersion=%1 版本 %2
AdditionalIcons=附加捷径：
CreateDesktopIcon=建立桌面捷径 (&D)
CreateQuickLaunchIcon=建立快速启动捷径 (&Q)
ProgramOnTheWeb=%1 的网站
UninstallProgram=解除安装 %1
LaunchProgram=启动 %1
AssocFileExtension=将 %1 与档案副档名 %2 产生关联 (&A)
AssocingFileExtension=正在将 %1 与档案副档名 %2 产生关联...
AutoStartProgramGroupDescription=启动：
AutoStartProgram=自动启动 %1
AddonHostProgramNotFound=%1 无法在您所选的资料夹中找到。%n%n您是否还要继续？