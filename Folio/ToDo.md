# Bugs

## PRs to merge
  * claude/testing-mi2oumdmt1930a09-01UedrH6555pU9PGoVQtUGxR
  * claude/photo-thumbnail-exif-batch-017wRTpGuj9VFZoZdm9K6eDm

  * claude/undo-system-command-snapshot-01THcUNNkQtWHFCq9VXHAtXT
  * claude/upgrade-dotnet-9-01AbA31B4RGykr18BeRrvVmr

  * claude/fix-todo-mi2p34zrbbtoc0jy-018EeQjq8QY1VrcK9HuztyfP

buggy:
  * claude/add-book-selector-dropdown-01X3tXgMCdehovLeKMDBCR21
  * claude/photo-zoom-animations-01QR37rW98ou4vMwAJc56ohP
  * claude/add-designer-splitter-013Zf5At6TmtGnqY37d3repR -- Crashes when drag the splitter up. Also not a particularly beautiful splitter.
  * claude/fix-photo-tags-display-019sXzPzr34pGMSDEsv8bowD  -- App becomes unresponsive when you change selection

  to do:
  * Capture the common drag and drop logic between DroppableImageDisplay.xaml.cs, PhotoGrid.xaml.cs, and PhotoGridFilters.xaml.cs

## Aspect ratios
* Pages flicker and relayout as images load
* Not all templates work at all screen sizes 

## Other page designer
* Moving to another page with keyboard doesn’t scroll the table of contents into view
* Page up/down should probably move only one page not a bunch
* Switching into dual page view mode loses your selection
* Some templates have * rows at the top and bottom, or * columns on the left and right. At best that’s redundant, but it might cause problems too.
* Page guidelines / outlines are only good for one sized page.
* Add a progress dialog to the print command
* Automatically adjust margins for the binding seam
* Undo command. Autosave.

## UI
* Template chooser comes up too slowly.
* The selection color in the table of contents is blue rather than gray

## Other
* Context menu doesn’t show page designer commands 
* Not all database images can be loaded, not even thumbnails. Crashes. Unsupported codec?

# Dialogs
* make dialogs resizable 

# Photo book 
* Have a mode to go full screen on one image in a book 
* output to HTML 
* Adaptive layout snap points -- switch to different layouts when sufficiently wide or tall
* Undo

# Code quality
* clean up the source code
* remove dead code
* reformat XAML
* Don't hard code all the configuration and directories
* Create an installer

# Look and feel 
* fix the tree view selection color on the main screen  
* make tree views look better, the icons are off  
* fix the color and spacing on radio button circles
* Render template chooser selection as gray like the photo grid.
* Fix the spaces between selected items in a photo grid. Perhaps corner radius is dependent on whether the next entry is selected. 

# Transition animations
* Moving selection in the photo grid (think excel) 
* navigating to a new mode  
* navigating to a different page 
