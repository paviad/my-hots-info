# Install

Clone the repo into `C:\MyProjects\MyHotsInfo` the path is hard coded in some places. You may clone to another location, but update the source code accordingly.

# Usage

`MyHotsCli` is the command line tool, it is the only thing that has some working functionality.

Build it and run it, it uses the (yet experimental) `System.CommandLine` library by Microsoft, and will display usage information when run without arguments (or with improper arguments)

## Examples

* `MyHotsCli scan --list` - Try to find locations of multi player replays (under My Documents) and list them in order.
* `MyHotsCli scan -s 2` - Scan all replays from location #2 in the list (from the previous example).
* `MyHotsCli qp joeschmoe` - Query your replays for a player named `joeschmoe` and show some statistics about them.
* `MyHotsCli scrape` - Scrape your Hots installation for talent information.
* `MyHotsCli scrape -i` - Install the scraped data into the database (can only be run after the previous example).
* `MyHotsCli qh chromie` - Show statistics about games with *Chromie*

# Other Client Types

## MAUI

The folder `MyHotsInfo` is a (mostly) blank MAUI app.