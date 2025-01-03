# EQPcreateFromBlock
Prove of Concept / sample code: In Plant 3D create an equipment and the nozzles automatically

This is sample code, use at own risk. Furthermore this is a prove of concept, so it only works for this specific example showing the principles that are needed to create a tool to convert AutoCAD blocks into Plant 3D equipment with nozzles with one click.

This example code is based on an example file (from grabcad.com) which was imported to Inventor. From there is was shared with AutoCAD using the ACC based Data Exchange (beta at date of writing: https://apps.autodesk.com/BIM360/en/Detail/Index?id=8886495673576738326&appLang=en&os=Web). In AutoCAD a native AutoCAD DWG block was received from the Data Exchange (see DWG in this repository).

In order to activate the Data Exchange tool in Plant 3D, modify the C:\Users\<username>\AppData\Roaming\Autodesk\ApplicationPlugins\AutoCAD Connector.bundle\PackageContents.xml (or similar, path might change over time). In this xml change the two occurances of Platform="AutoCAD" -> Platform="AutoCAD*"

So all written above leads to an AutoCAD block of a vessel with nozzles available in Plant 3D, so this is the starting point. It finally doesn't matter if the block is coming from the Data Exchange or if it was created in a different way, just some conditions need to be fullfilled in order to make the code work:

- the vessel block contains all geometry, while the nozzles are sub-blocks, but there are no nested blocks
- the vessel block name represents the equipment name (there might be also other ways to receive information, because the central blocks has block parameters)
- the nozzle block names need to contain all information that is needed on the Plant 3D side in order to assign the correct nozzle, e.g. size, pressure class, standard, description, ..
- the nozzle block origin must represent the connection point (port) in Plant 3D
- the nozzle block must be rotation symmetrical, because the connection direction is detected by the center of the bounding box of the nozzle block. This condition could be replace by an additional drawing element in Inventor, e.g. a point or a line



