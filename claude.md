  # Folio - WPF Photo Viewer

  ## Project Overview
  A WPF desktop application for viewing, organizing, and managing photos with tagging capabilities.

  ## Architecture & Patterns
  - WPF with code-behind (not using MVVM)
  - Direct event handling in XAML code-behind files
  - Resource dictionaries for shared styles and templates
  - Most of the active development is in the Folio/book directory 

  ## Design System
  - **Fonts**: Segoe UI Variable (primary font family)
  - **Spacing**: Follow Microsoft's 40px grid system
    - Everything is a multiple of 4
    - Outer padding on dialog: 32px
    - 24px after a section heading
    - 16px Vertically between a form control and the next row of controls on the form
    - 8px horizontally between controls on the same row
    - 24px above and below the OK and cancel button in a dialog
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
  - Don't use doc comments (///). Just use regular comments (//), and don't put XML inside comments.
  - When writing tests, don't put in comments for // Arrange, // Act, // Assert. Just write the code.

  ## Build Commands
  - dotnet build --no-incremental --verbosity q
  - Or if you have to use MSBuild: `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" Folio.sln -property:Configuration=Debug -verbosity:minimal`

  ## Test commands
  - dotnet test "Folio.Tests\bin\Debug\net8.0-windows\Folio.Tests.dll" --verbosity normal
  - Or: dotnet test Folio.Tests\Folio.Tests.csproj --verbosity normal

  ## Logging
  - Uses **Serilog** for structured logging
  - Log location: `%LocalAppData%\Folio\logs\folio-YYYY-MM-DD.log`
    - Typically: `C:\Users\[username]\AppData\Local\Folio\logs\`
  - Rolling daily log files (retains last 7 days)
  - Logs image load performance metrics:
    - Duration (ms), pixel count, memory usage, timestamps
  - Configured in App.xaml.cs, logging added to ImageInfoWPF.cs

  ## Using Seq to view logs

  1. Open Seq in your browser: http://localhost:5341
  2. Run your Folio application - logs will now appear in both:
    - Rolling files: %LocalAppData%\Folio\logs\folio-YYYY-MM-DD.log
    - Seq UI: http://localhost:5341

  ## Important Conventions
  - Always prefer editing existing files over creating new ones
  - Don't create documentation files unless explicitly requested
  - Match existing spacing and layout patterns from SelectFolders dialogs
  - Use StaticResource for all styles and brushes
  - Always compile the project before completing a task.
