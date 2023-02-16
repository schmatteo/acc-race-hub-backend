# acc-race-hub-backend

This is a C# rewrite of [acc-race-hub](https://github.com/schmatteo/acc-race-hub)'s backend.
It's not finished yet, it only supports a limited set of features for now.

## Features

### Implemented

- [X] Listening for race and qualifying results in a directory
- [X] Processing race and qualifying results
- [X] Inserting results into a database
- [X] Drivers' and constructors' points calculation (and inserting into database)
- [X] Drop round calculation
- [X] Reading MongoDB connection string from file in appdata, or from command line argument

### TODO

- [ ] Teams points calculation
- [ ] Ability to change results directory or upload them through a UI
- [ ] Custom logger to file
- [ ] Desktop UI
- [ ] Tools that would help in creating entrylist and teams files
- [ ] REST API (potentially as a different assembly)

## Usage
1. Run the executable from command line with argument `--mongourl <connection string>`
(replace <connection string> with your MongoDB connection string), 
this way the program creates a config file, so you never have to run it 
with the argument again, unless you want to change the connection string.

To upload race/qualifying result, simply put the .json file in the same directory as the executable.
