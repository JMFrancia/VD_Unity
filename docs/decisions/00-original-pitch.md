#VoidDay

A rapid game prototype inspired by HayDay

#Overview

##Tech specs

- 2D, simple sprites, start with placeholders, make it easy to replace later  
- Use phaser or similar, compile to HTML5  
- Meant to be a prototype for mobile game  
  - Use portrait-mode phone dimensions for screen  
  - Controls are all touch/drag with mouse, no keyboard

##Design

- Main point of reference is HayDay  
- View is top-down, looking at large open grassy area

### Core Loop Overview

- VoidDay is a farming game that emphasizes pipeline management.   
- The player builds generator stations to produce raw goods or convert raw goods into processed goods, then sells them by fulfilling orders.   
- Player uses money from the sales to build new stations or upgrade stations.   
- Certain game actions (collecting good, fulfilling order, and more) grant XP, which allows player to level up their farm. Leveling up the farm unlocks new upgrades and building types  
- Player can also receive VoidPet Eggs, which hatch into collectable VoidPets. VoidPets can be assigned to generator stations to auto-collect for the player, and also grant passive bonuses.  
  - VoidPets within range of one another will develop relationships that grant further bonuses (specific to the two of them)  
- The game will also have occasional random world events. These events may just be for flavor, or some may have temporary effects

##Player has...

- Resources (these are just numbers)  
  - Money  
  - Raw ingredients (of various types)  
  - Processed goods (of various types)  
- Stations (buildings placed on the map)  
  - Generator Stations  
    - Can place orders that process a recipe  
      - Recipe: Combine some number of resources to create a new resource, sometimes with a timer  
        - Ex: A bakery has a recipe to convert 1 wheat into 1 bread with a 30 second timer  
        - Ex: A bakery has a recipe to convert 1 wheat and 1 corn into 1 cornbread with a 90 second timer  
        - Etc.  
    - Orders can be queued  
  - Upgrade Stations  
    - Place to purchase specific universal upgrades  
  - VoidPet Stations  
    - Provide area bonus to Voidpets nearby (may have conditions / limits)  
- VoidPets (these are collectable familiars that can be assigned to stations)  
  - Voidpet assigned to a station will auto-collect at that station  
  - Each VoidPet has AT LEAST one trait granting special abilities  
    - Ex: such as speed boost for the station they are assigned, or universal cost decrease for specific resource  
    - VoidPet may have a trait that affects nearby assigned VoidPets as well  
  - VoidPets placed within range of one another will form relationships, granting new trait bonuses specific to the two of them

##Player can...

- Move camera around, zoom in/out  
- Build out their farm   
  - Build stations  
  - Move stations already built  
- Grow, harvest, and process goods at stations  
  - Create a station order at a station for a resource   
  - Collect the station order from the station when its complete  
- Upgrade their farm  
  - Purchase station-specific upgrades (at said station)  
  - Purchase universal upgrades (from an upgrade menu)  
- Gain experience and level up  
  - Experience gained through most actions  
  - Level up unlocks new game content  
    - Increases station caps  
    - Increases order caps  
    - Unlocks game world events  
- Collect VoidPet Familiars  
  - Receive VoidPet Eggs   
  - Hatch egg to receive VoidPet  
  - Assign or Un-Assign VoidPets to generator stations  
- Fulfill orders  
  - View orders from order station  
  - Fulfill order to receive cash reward  


#UI

## Main Game HUD

- Station Build Menu button (bottom left)  
  - Tapping opens build menu, tap again to close  
- Money amount (top right)  
  - Tapping opens total resource popup, tap again to close  
- Debug menu button (top left)  
  - Tapping opens debug menu, tap again to close

## Popups  


- Level up  
  - Displays congrats message, new level, list of what unlocks come with this level, and a list of rewards  
    - Some unlock types:  
      - New station upgrades available for purchase  
      - New universal upgrades available for purchase  
      - Higher caps on station count (per station type)  
      - New station types  
- Hatch egg  
  - Shows egg, player taps to hatch, granting them a new voidpet

### Generic Popups (these are purely data-driven)

- Generic text popup that appears in a “dialogue” window to explain things, give the player updates, etc.  
- Event popups  
- Toasts that appear temporarily in corner of the screen to give the player event updates

### Hatch VoidPet Popup

- This popup shows an egg, which you tap to convert your egg into a new VoidPet

### Total resource popup

- Displays a list of all resources, and how much you own of each

## Menus

- Station build menu  
  - Shows the stations that are available to build, and their costs (in money)  
  - Stations may be locked (showing lock icon, and appearing in grayscale) because player level is not high enough  
- VoidPet Menu  
  - Shows all voidpets currently collected. Player can tap one to see open Voidpet Details Popup  
    - VoidPet Details Popup  
      - Shows picture of VoidPet  
      - Shows blurb about VoidPet  
      - Shows quote from VoidPet (in italics, with quotation marks)  
      - Shows rarity of VoidPet  
      - Shows traits of VoidPet  
      - Shows current station assignment of VoidPet

#Development Principles

## Everything is data-driven and tunable

- Strict MVC  
- All data stored in JSON  
- All data Tunable via separate screen, results permanently saved to JSON  
- Tunable data includes (but not limited to)  
- Starting resources  
- All station stats  
  - Timers  
  - Upgrades  
  - Etc.  
- Player levels  
  - Exp. requirements  
  - Unlocks  
  - Etc.  
- Experience  
  - How much experience different actions grant  
  - Etc.

## Everything is event-driven

- No tightly coupled systems  
- Events emitted by...  
  - Every player choice  
  - Every timer start / completion  
  - Etc.
