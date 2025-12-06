# Bugs

## PRs to merge
  * claude/upgrade-dotnet-9-01AbA31B4RGykr18BeRrvVmr

buggy:
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
