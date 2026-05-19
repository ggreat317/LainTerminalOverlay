First non-web application lol. It's a simple "pop-up" of a character from a show I like that appears around the corner of your terminal.
Made during procastination session of my really cool main project and really fun intern work.
10/10, would recommend! ( but you should probably have some anti-virus look at the code first [ i mean it won't find anything, but a safety precaution before running anything ] )

FOR INSTALLATION:
( assuming everything is windows here )
1. clone repo
2. go into bin/Release/net10.0-windows/win-x64/TerminalOverlay.exe ( make a shortcut to it, for ease of access )
3. run it

FOR EDITING
( go in terminal [ into repo ], still assuming windows lol )
1. edit in Visual Studio or something, the assets folder would be pretty easy to change, just keep them all named lain
2. run 'dotnet publish -c Release -r win-x64 --self-contained false' ( if want .NET install req for other users )
2. run 'dotnet publish -c Release -r win-x64 --self-contained true' ( if want .NET install req for other users )
