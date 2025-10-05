  # Folio - WPF Photo Viewer

  ## Project Overview
  A WPF desktop application for viewing, organizing, and managing photos with tagging capabilities.

  ## Architecture & Patterns
  - WPF with code-behind (not using MVVM)
  - Direct event handling in XAML code-behind files
  - Resource dictionaries for shared styles and templates

  ## Design System
  - **Fonts**: Segoe UI Variable (primary font family)
  - **Spacing**: Follow Microsoft's 40px grid system
    - Outer padding: 32px
    - Section spacing: 24px
    - Item spacing: 12px or 8px
    - Use multiples of 4 or 8
  - **Typography Scale**:
    - Title: 28px SemiBold (TitleTextBlockStyle)
    - Subtitle/Headers: 20px SemiBold (FolderHeaderTextBlockStyle)
    - Body: 14px Regular (UiBodyTextBlockStyle)
  - **Colors**:
    - darkGray: #FF333333 (backgrounds)
    - midGray: #FF414141 (panels, tree views)
    - almostBlack: #FF202020
    - White foreground for text

  ## Code Style
  - Use existing style resources from MiscResources.xaml
  - Dialogs use WindowStyle="None" (no title bar)
  - Buttons use ButtonStyle1

  ## Build Commands
  - Build: `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
  pictureviewer.sln -property:Configuration=Debug -verbosity:minimal`
  - Use MSBuild, not dotnet CLI

  ## Important Conventions
  - Always prefer editing existing files over creating new ones
  - Don't create documentation files unless explicitly requested
  - Match existing spacing and layout patterns from SelectFolders dialogs
  - Use StaticResource for all styles and brushes
