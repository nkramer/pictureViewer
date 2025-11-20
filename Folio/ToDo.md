# Bugs

## PRs to merge
  * claude/add-book-selector-dropdown-01X3tXgMCdehovLeKMDBCR21
  * claude/add-designer-splitter-013Zf5At6TmtGnqY37d3repR
  * claude/add-drag-drop-feedback-01S8dwju5JA2n9mp5dgnFnT5
  * claude/fix-context-menu-commands-01G4KNaQncTtGgXX6A7TpcCd
  * claude/fix-photo-tags-display-019sXzPzr34pGMSDEsv8bowD
  * claude/fix-todo-mi2p34zrbbtoc0jy-018EeQjq8QY1VrcK9HuztyfP
  * claude/fix-tree-view-scroll-01JPTP7FNF9w1JVxefaRHYpd
  * claude/increase-size-019W3vfKuVzrAN81feQ1k2Ly
  * claude/make-dialogs-movable-01Sxz2BJBeFicTCg8GmsvunG
  * claude/move-toc-listbox-left-01197z71Yi8sLwzyCJ4WSgvK
  * claude/photo-thumbnail-exif-batch-017wRTpGuj9VFZoZdm9K6eDm
  * claude/photo-zoom-animations-01QR37rW98ou4vMwAJc56ohP
  * claude/replace-messagebox-basedialog-01CpfzJUYG5iPNooQbco5ufo
  * claude/research-photobook-pricing-01AccbAeHwYDAJqtjPVBahC3
  * claude/template-chooser-dialog-01TdJoChS3Kic3RL4dxHDZpL
  * claude/testing-mi2oumdmt1930a09-01UedrH6555pU9PGoVQtUGxR
  * claude/undo-system-command-snapshot-01THcUNNkQtWHFCq9VXHAtXT
  * claude/upgrade-dotnet-9-01AbA31B4RGykr18BeRrvVmr

## Aspect ratios
* 6p0h6v0t Becomes unsolvable once you drop a 4:3 Portrait image in it. Overconstrained Overconstrained Overconstrained.
* 4p2h2v1t Unsolvable (always? Sometimes?) w/ 4:3. Overconstrained Underconstrained Overconstrained.
* 9p9h0v0t Unsolvable (always? Sometimes?) w/ 4:3. Overconstrained Overconstrained Overconstrained'
* 3p2h1v0t is stuck – Renders a portrait as a landscape. 3p2h1v0t_2 also. 4p2h2v1t has the same problem.
* I saw a timing issue on 3p3h0v1t where the top left image was rendered in the wrong place, too far to the right and overlapping with the text.
* Pages flicker and relayout as images load
* Red border around images

## Other page designer
* Moving to another page with keyboard doesn’t scroll the table of contents into view
* Page up/down should probably move only one page not a bunch
* Switching into dual page view mode loses your selection
* First page in dual page mode renders wacky. Old image can still be there, or it can render only a single page.
* Some templates have * rows at the top and bottom, or * columns on the left and right. At best that’s redundant, but it might cause problems too.
* Page guidelines / outlines are only good for one sized page.

## UI
* Turn right rail into the left rail
* Template chooser is not a true dialog
* Template chooser comes up too slowly.
* The selection color in the table of contents is blue rather than gray

## Other
* Context menu doesn’t show page designer commands 
* Not all database images can be loaded, not even thumbnails. Crashes. Unsupported codec?

# Dialogs
* make dialogs movable 
* make dialogs resizable 

# Photo book 
* Support arbitrary page aspect ratios 
* Auto decide the photo aspect ratio 
* allow mixing photo aspect ratios on the same page 
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
* make the template chooser dialog into a true dialog, and use a dark background
* Render template chooser selection as gray like the photo grid.
* Fix the spaces between selected items in a photo grid. Perhaps corner radius is dependent on whether the next entry is selected. 

# Drag drop 
* give feedback about what you're dragging. Dragging tags. Dragging images in PhotoBook. 

# Transition animations
* Moving selection in the photo grid (think excel) 
* navigating to a new mode  
* navigating to a different page 
