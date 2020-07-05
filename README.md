# Autodocking-2

A script for Space Engineers that will automatically control and dock your in-game ship to a home connector.

![A cool gif about docking](https://raw.githubusercontent.com/ksqk34/Autodocking-2/master/Autodocking%202/gifs/DockingSequence.gif)

## Showcase Video

[![Showcase Video](http://img.youtube.com/vi/Ogm4yzAaqEg/0.jpg)](http://www.youtube.com/watch?v=Ogm4yzAaqEg)

## How the script works

### Overview

This section will explain in a bit more detail how the script operates. In the script settings (by clicking "Edit" in the programming block), you can enable extra info mode. With this on, more information will be relayed to you as the script operates. This can help give you some insight on how it works.

### Landing sequence

When landing, the ship goes through 4 major phases. These are done in sequence and I'll explain each below. Every time the script runs, it assesses the situation to determine what phase it is in. This means that the ship does not store any "state" between frames, it is constantly re-assessed, allowing a much higher level of robustness.

#### 1. Alignment

In this phase, the ship will simply hover and rotate. It will rotate using pitch and roll so that the connectors line up. To progress to the next phase, the connectors need to be aligned to within 15 degrees.

#### 2. Correction

If the ship is behind the target connector, it needs to fly to be above it. It will do this "carelessly" - not worrying about stopping at the exact height level. To progress to the next stage, the ship needs to be at least 5 meters above the connector (roughly. This number changes with the speed setting).

#### 3. Waypointed Flight

The flight computer places an internal waypoint at a distance of 4 meters above the target connector (measured from connector center to connector center). The ship will fly directly at this, using "max possible thrust". The ship knows when to apply reverse burn because it works out the "max possible reverse thrust" by taking into account the thrust needed for gravity, the amount of thrusters in the direction, and the ship's mass. Any sideways velocities will be cancelled out during acceleration. The ship will aim to get to cruising at the top speed if it can. When the ship is within 4 meters of the connector (changes depending on speed setting), it progresses to the next stage.

#### 4. Precision Landing

While the "Precision Landing" name suggests a slow gentle landing, this phase is often very fast. This is because the ship only slows if it deems something is going wrong. An optimal landing is one where it is going at "max possible reverse thrust", and by the time it has reached speed 0, it is perfectly touching the connector. This is called a "suicide burn" by Elon Musk. The ship looks forward in time, sees where it will end up, looks at how badly that is wrong and adjusts accordingly, hence the precision.

### Saving locations

The script defines a location as: A ship connector, platform connector, and a set of arguments that relate to this location. If you dock at a new "location", this will be added to the location list. The location list can be seen by enabling extra info mode, and pressing recompile.  

Arguments cannot have the following characters in: ` ¬ ; #  
This is because they're used as delimeters when saving.

When adding a new argument, the script will remove any occurances of the argument elsewhere, and add it to the set of arguments relating to that location.  

## The Shiny New Version

After not being able to maintain version 1 for a while, when I came back, it was a buggy mess. Even without the bugs there were flaws in the base code. Because of this, I decided to totally rewrite it with a new system.

This new version has an accurate model of the ship thrusts, and will use this to calculate the maximum possible acceleration in a direction. Knowing the maximum acceleration allows the ship to know when to apply reverse thrust. And so it can safely boost toward the target connector knowing exactly when it'll have to do a reverse burn.

This is different to version 1, because version 1 did not know any of this. It simply accelerated by some arbitrary amount until an arbitrary top speed, then took a guess at when it should probably decelerate. As you can guess, this lack of a model caused issues.

My rotation code was also buggy. When the ship was very far from the (0,0,0) world coordinate, the numbers got large enough to have floating point issues. For this reason I changed my method and also introduced some of Whiplash's code. He wrote a great PID controller which I repurposed to control my pitch, yaw and roll.

My old script had some great bugfixes from the community. I am glad they could spend the time looking through the code to find the fixes, and help others out with it. I think the Sparks of the Future update may have broken these for good though.

![Another cool gif about docking](https://raw.githubusercontent.com/ksqk34/Autodocking-2/master/Autodocking%202/gifs/DockingSequence4.gif)

## Limitations

Because this version was recently programmed, I prioritised code quality and reliability over features. I believe this was the right call, however features are still important, especially since my version 1 currently has more features than version 2.

The upcoming features are highlighted in the To-do section of this readme.

Code limitations include the unnecesary use of memory allocation. At some point I plan to go through and change a load of local variables to global ones. While this will make it less readable, the trade-off is a good one.

More comments need to be added. I'm aware my last script had lots of people looking through it, so I'd like to provide these with a nice readable set of code.

It's not as customizable as I'd like - There's only 7 changeable parameters. These can't be changed using the "Custom Data" field of the programming block either which is pretty standard in most scripts.

Testing - while I have tested this on a fair amount of ships, I am but a one-man-band. This game allows players to create a huge variety of ships, I fear the boundaries of what I have thought up will be pushed too far by people.

## To-do

- Landing on moving ships.
- Waypointed landings.
- Fix breakneck speed overshoot issue.
- Add remote control collision avoidance.
- Add option for rotating as it lowers onto connector.
- Look up how to calculate gyro spin speed, ready for flip-and-burn.
- Add "API" - activate timer with tag, once docked.
- Allow not all 6 directions having thrust.
- Allow clearing of memory.
