


ControlMyMonitor v1.26
Copyright (c) 2017 - 2020 Nir Sofer
Web site: http://www.nirsoft.net



Description
===========

ControlMyMonitor allows you view and modify the settings of your monitor
(Also known as 'VCP Features'), like brightness, contrast, sharpness,
red/green/blue color balance, and more... You can modify the monitor
settings from the GUI and from command-line. You can also export all
settings of your monitor into a configuration file and then later load
the same configuration back into your monitor.



System Requirements
===================


* Any version of Windows, starting from Windows Vista and up to Windows
  10.
* Hardware that supports DDC/CI.



Versions History
================


* Version 1.26:
  o When ControlMyMonitor fails to get the current monitor settings,
    error code is now displayed in the status bar.

* Version 1.25:
  o Added 'Put Icon On Tray' option.

* Version 1.20:
  o Added /SwitchValue command-line option, which allows you to
    switch between multiple values.
  o For example, in order to switch the monitor off when it's turned
    on and switch it on when it's turned off, use the following command:
    (On some monitors you should use 4 instead of 5)
    ControlMyMonitor.exe /SwitchValue "\\.\DISPLAY1\Monitor0" D6 1 5

* Version 1.17:
  o When pressing F5 (Refresh) the refresh process is smoother,
    keeping the last selected item.
  o Added 'Load Selected Monitor On Start' option.

* Version 1.16:
  o Added /GetValue command-line option, which returns the current
    value of the specified VCP Code, for example:
    ControlMyMonitor.exe /GetValue "\\.\DISPLAY1\Monitor0" 10
    echo %errorlevel%

* Version 1.15:
  o Added /SetValueIfNeeded command-line option, which sets a value
    only if the current value is different from the value you want to set.

* Version 1.12:
  o Added 'Add Header Line To CSV/Tab-Delimited File' option (Turned
    on by default).

* Version 1.11:
  o Added /smonitors command-line option to save the monitors list
    into a text file.
  o Added 'Save All Items' option (Shift+Ctrl+S).

* Version 1.10:
  o Added save command-line options (/scomma, /stab , /shtml, and so
    on...) to export the current monitor settings into a file.
  o Added support for JSON file in 'Save Selected Items' option.

* Version 1.05:
  o Added 'Refresh Monitors List' option (Ctrl+F5).

* Version 1.00 - First release.



Start Using ControlMyMonitor
============================

ControlMyMonitor doesn't require any installation process or additional
DLL files. In order to start using it simply run the executable file -
ControlMyMonitor.exe

After running ControlMyMonitor, the current settings of your monitor are
displayed in the main window. If you have multiple monitors, you can
choose another monitor from the monitor combo-box below the toolbar.
In order to modify a single item, select the item that you want to
change, and then double click the item (or press the F6 key). You can
also increase or decrease the current value by using the 'Increase Value'
or 'Decrease Value' options (Under the Action menu). You can also
increase/decrease values by using the mouse wheel, according to the
selected option in Options -> Change Value With Mouse Wheel. By default,
the mouse wheel feature is active when you hold down the Ctrl key.



Restore Factory Defaults
========================

There are some write-only items that allow you to restore the factory
defaults of the monitor. In order to activate these items, you have to
set the value to 1.



Save/Load Config
================

ControlMyMonitor allows you to export all read/write properties into a
simple text file and then later load these properties back to the
monitor. You can find the save/load config feature under the File menu
('Save Monitor Config' and 'Load Monitor Config').



Command-Line Options
====================

If you have only one monitor, you can use 'Primary' as your monitor
string in all command-line options.
If you have multiple monitors, you have to find a string that uniquely
identifies your monitor. Open ControlMyMonitor , select the desired
monitor and then press Ctrl+M (Copy Monitor Settings). Paste the string
from the clipboard into notepad or other text editor. You'll see
something like this:

Monitor Device Name: "\\.\DISPLAY1\Monitor0"
Monitor Name: "22EA53"
Serial Number: "402CFEZE1200"
Adapter Name: "Intel(R) HD Graphics"
Monitor ID: "MONITOR\GSM59A4\{4d36e96e-e325-11ce-bfc1-08002be10318}\0012"

You can use any string from this list as long as the other monitors on
your system have different values for the same property.



/SetValue <Monitor String> <VCP Code> <Value>
Sets the value of the specified VCP Code for the specified monitor.

Here's some examples:

Set the brightness of primary monitor to 70:
ControlMyMonitor.exe /SetValue Primary 10 70

Set the contrast of the monitor with serial number 102ABC335 to 65:
ControlMyMonitor.exe /SetValue "102ABC335" 12 65

Restore factory defaults to the \\.\DISPLAY1\Monitor0 monitor:
ControlMyMonitor.exe /SetValue "\\.\DISPLAY1\Monitor0" 04 1

Turn on the \\.\DISPLAY2\Monitor0 monitor:
ControlMyMonitor.exe /SetValue "\\.\DISPLAY2\Monitor0" D6 1

Turn off the \\.\DISPLAY2\Monitor0 monitor: (On some monitors you should
set the value to 4 instead of 5)
ControlMyMonitor.exe /SetValue "\\.\DISPLAY2\Monitor0" D6 5

Change the input source of \\.\DISPLAY3\Monitor0 monitor:
ControlMyMonitor.exe /SetValue "\\.\DISPLAY3\Monitor0" 60 3

/SetValueIfNeeded <Monitor String> <VCP Code> <Value>
This command is similar to /SetValue , but it actually sets the value
only if the current value is different from the specified value.

/ChangeValue <Monitor String> <VCP Code> <Value>
Increases/decreases the value of the specified VCP Code for the specified
monitor.

Here's some examples:

Increase the brightness of the primary monitor by 5%:
ControlMyMonitor.exe /ChangeValue Primary 10 5

Decrease the contrast of the \\.\DISPLAY1\Monitor0 monitor by 5%:
ControlMyMonitor.exe /ChangeValue "\\.\DISPLAY1\Monitor0" 12 -5

/SwitchValue <Monitor String> <VCP Code> <Value1> <Value2> <Value3>...
Switch between the specified 2 or more values.

For example, this command switches the monitor between off (5) and on (1)
state:
ControlMyMonitor.exe /SwitchValue "\\.\DISPLAY1\Monitor0" D6 1 5

The following command switches between 3 brightness values - 30%, 50%,
90% :
ControlMyMonitor.exe /SwitchValue "\\.\DISPLAY1\Monitor0" 10 30 50 90

/GetValue <Monitor String> <VCP Code>
Return the value of the specified VCP Code for the specified monitor.

Example for batch file:
ControlMyMonitor.exe /GetValue "\\.\DISPLAY1\Monitor0" 10
echo %errorlevel%

/SaveConfig <Filename> <Monitor String>
Saves all read+write values of the specified monitor into a file.
For example:
ControlMyMonitor.exe /SaveConfig "c:\temp\mon1.cfg" Primary
ControlMyMonitor.exe /SaveConfig "c:\temp\mon1.cfg"
"\\.\DISPLAY2\Monitor0"

/LoadConfig <Filename> <Monitor String>
Loads all values stored in configuration file into the specified monitor.
For example:
ControlMyMonitor.exe /LoadConfig "c:\temp\mon1.cfg" Primary
ControlMyMonitor.exe /LoadConfig "c:\temp\mon1.cfg"
"\\.\DISPLAY2\Monitor0"

/stext <Filename> <Monitor String>
Save the current monitor settings into a simple text file.

/stab <Filename> <Monitor String>
Save the current monitor settings into a tab-delimited text file.

/scomma <Filename> <Monitor String>
Save the current monitor settings into a comma-delimited text file (csv).

/shtml <Filename> <Monitor String>
Save the current monitor settings into HTML file (Horizontal).

/sverhtml <Filename> <Monitor String>
Save the current monitor settings into HTML file (Vertical).

/sxml <Filename> <Monitor String>
Save the current monitor settings into XML file.

/sjson <Filename> <Monitor String>
Save the current monitor settings into JSON file.

/smonitors <Filename>
Save the current monitors list into a simple text file.

For all save command-line options, you can specify empty filename in
order to send the data to stdout, for example:
ControlMyMonitor.exe /scomma "" | more



Get value of the specified VCP Code using GetNir tool
=====================================================

There is also an option to get the current value of the specified VCP
Code with GetNir tool. The value is sent to stdout.
For example, the following command sends the current monitor brightness
to stdout:
ControlMyMonitor.exe /stab "" | GetNir "Current Value" "VCPCode=10"



Translating ControlMyMonitor to other languages
===============================================

In order to translate ControlMyMonitor to other language, follow the
instructions below:
1. Run ControlMyMonitor with /savelangfile parameter:
   ControlMyMonitor.exe /savelangfile
   A file named ControlMyMonitor_lng.ini will be created in the folder of
   ControlMyMonitor utility.
2. Open the created language file in Notepad or in any other text
   editor.
3. Translate all string entries to the desired language. Optionally,
   you can also add your name and/or a link to your Web site.
   (TranslatorName and TranslatorURL values) If you add this information,
   it'll be used in the 'About' window.
4. After you finish the translation, Run ControlMyMonitor, and all
   translated strings will be loaded from the language file.
   If you want to run ControlMyMonitor without the translation, simply
   rename the language file, or move it to another folder.



License
=======

This utility is released as freeware. You are allowed to freely
distribute this utility via floppy disk, CD-ROM, Internet, or in any
other way, as long as you don't charge anything for this and you don't
sell it or distribute it as a part of commercial product. If you
distribute this utility, you must include all files in the distribution
package, without any modification !



Disclaimer
==========

The software is provided "AS IS" without any warranty, either expressed
or implied, including, but not limited to, the implied warranties of
merchantability and fitness for a particular purpose. The author will not
be liable for any special, incidental, consequential or indirect damages
due to loss of data or any other reason.



Feedback
========

If you have any problem, suggestion, comment, or you found a bug in my
utility, you can send a message to nirsofer@yahoo.com
