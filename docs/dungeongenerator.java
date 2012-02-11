import java.util.*; //and for getting today's date
 
public class dungen{
	//max size of the map
	private int xmax = 80; //80 columns
	private int ymax = 25; //25 rows
 
	//size of the map
	private int xsize = 0;
	private int ysize = 0;
 
	//number of "objects" to generate on the map
	private int objects = 0;
 
	//define the %chance to generate either a room or a corridor on the map
	//BTW, rooms are 1st priority so actually it's enough to just define the chance
	//of generating a room
	private int chanceRoom = 75; 
	private int chanceCorridor = 25;
 
	//our map
	private int[] dungeon_map = { };
 
	//the old seed from the RNG is saved in this one
	private long oldseed = 0;
 
	//a list over tile types we're using
	final private int tileUnused = 0;
	final private int tileDirtWall = 1; //not in use
	final private int tileDirtFloor = 2;
	final private int tileStoneWall = 3;
	final private int tileCorridor = 4;
	final private int tileDoor = 5;
	final private int tileUpStairs = 6;
	final private int tileDownStairs = 7;
	final private int tileChest = 8;
 
	//misc. messages to print
	private String msgXSize = "X size of dungeon: \t";
	private String msgYSize = "Y size of dungeon: \t";
	private String msgMaxObjects = "max # of objects: \t";
	private String msgNumObjects = "# of objects made: \t";
	private String msgHelp = "";
	private String msgDetailedHelp = "";
 
 
	//setting a tile's type
	private void setCell(int x, int y, int celltype){
		dungeon_map[x + xsize * y] = celltype;
	}
 
	//returns the type of a tile
	private int getCell(int x, int y){
		return dungeon_map[x + xsize * y];
	}
 
	//The RNG. the seed is based on seconds from the "java epoch" ( I think..)
	//perhaps it's the same date as the unix epoch
	private int getRand(int min, int max){
		//the seed is based on current date and the old, already used seed
		Date now = new Date();
		long seed = now.getTime() + oldseed;
		oldseed = seed;
 
		Random randomizer = new Random(seed);
		int n = max - min + 1;
		int i = randomizer.nextInt(n);
		if (i < 0)
			i = -i;
		//System.out.println("seed: " + seed + "\tnum:  " + (min + i));
		return min + i;
	}
 
	private boolean makeCorridor(int x, int y, int lenght, int direction){
		//define the dimensions of the corridor (er.. only the width and height..)
		int len = getRand(2, lenght);
		int floor = tileCorridor;
		int dir = 0;
		if (direction > 0 && direction < 4) dir = direction;
 
		int xtemp = 0;
		int ytemp = 0;
 
		switch(dir){
		case 0:
		//north
			//check if there's enough space for the corridor
			//start with checking it's not out of the boundaries
			if (x < 0 || x > xsize) return false;
			else xtemp = x;
 
			//same thing here, to make sure it's not out of the boundaries
			for (ytemp = y; ytemp > (y-len); ytemp--){
				if (ytemp < 0 || ytemp > ysize) return false; //oh boho, it was!
				if (getCell(xtemp, ytemp) != tileUnused) return false;
			}
 
			//if we're still here, let's start building
			for (ytemp = y; ytemp > (y-len); ytemp--){
				setCell(xtemp, ytemp, floor);
			}
			break;
		case 1:
		//east
				if (y < 0 || y > ysize) return false;
				else ytemp = y;
 
				for (xtemp = x; xtemp < (x+len); xtemp++){
					if (xtemp < 0 || xtemp > xsize) return false;
					if (getCell(xtemp, ytemp) != tileUnused) return false;
				}
 
				for (xtemp = x; xtemp < (x+len); xtemp++){
					setCell(xtemp, ytemp, floor);
				}
			break;
		case 2:
		//south
			if (x < 0 || x > xsize) return false;
			else xtemp = x;
 
			for (ytemp = y; ytemp < (y+len); ytemp++){
				if (ytemp < 0 || ytemp > ysize) return false;
				if (getCell(xtemp, ytemp) != tileUnused) return false;
			}
 
			for (ytemp = y; ytemp < (y+len); ytemp++){
				setCell(xtemp, ytemp, floor);
			}
			break;
		case 3:
		//west
			if (ytemp < 0 || ytemp > ysize) return false;
			else ytemp = y;
 
			for (xtemp = x; xtemp > (x-len); xtemp--){
				if (xtemp < 0 || xtemp > xsize) return false;
				if (getCell(xtemp, ytemp) != tileUnused) return false; 
			}
 
			for (xtemp = x; xtemp > (x-len); xtemp--){
				setCell(xtemp, ytemp, floor);
			}
			break;
		}
 
		//woot, we're still here! let's tell the other guys we're done!!
		return true;
	}
 
	private boolean makeRoom(int x, int y, int xlength, int ylength, int direction){
		//define the dimensions of the room, it should be at least 4x4 tiles (2x2 for walking on, the rest is walls)
		int xlen = getRand(4, xlength);
		int ylen = getRand(4, ylength);
		//the tile type it's going to be filled with
		int floor = tileDirtFloor; //jordgolv..
		int wall = tileDirtWall; //jordv????gg
		//choose the way it's pointing at
		int dir = 0;
		if (direction > 0 && direction < 4) dir = direction;
 
		switch(dir){
		case 0:
		//north
			//Check if there's enough space left for it
			for (int ytemp = y; ytemp > (y-ylen); ytemp--){
				if (ytemp < 0 || ytemp > ysize) return false;
				for (int xtemp = (x-xlen/2); xtemp < (x+(xlen+1)/2); xtemp++){
					if (xtemp < 0 || xtemp > xsize) return false;
					if (getCell(xtemp, ytemp) != tileUnused) return false; //no space left...
				}
			}
 
			//we're still here, build
			for (int ytemp = y; ytemp > (y-ylen); ytemp--){
				for (int xtemp = (x-xlen/2); xtemp < (x+(xlen+1)/2); xtemp++){
					//start with the walls
					if (xtemp == (x-xlen/2)) setCell(xtemp, ytemp, wall);
					else if (xtemp == (x+(xlen-1)/2)) setCell(xtemp, ytemp, wall);
					else if (ytemp == y) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y-ylen+1)) setCell(xtemp, ytemp, wall);
					//and then fill with the floor
					else setCell(xtemp, ytemp, floor);
				}
			}
			break;
		case 1:
		//east
			for (int ytemp = (y-ylen/2); ytemp < (y+(ylen+1)/2); ytemp++){
				if (ytemp < 0 || ytemp > ysize) return false;
				for (int xtemp = x; xtemp < (x+xlen); xtemp++){
					if (xtemp < 0 || xtemp > xsize) return false;
					if (getCell(xtemp, ytemp) != tileUnused) return false;
				}
			}
 
			for (int ytemp = (y-ylen/2); ytemp < (y+(ylen+1)/2); ytemp++){
				for (int xtemp = x; xtemp < (x+xlen); xtemp++){
 
					if (xtemp == x) setCell(xtemp, ytemp, wall);
					else if (xtemp == (x+xlen-1)) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y-ylen/2)) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y+(ylen-1)/2)) setCell(xtemp, ytemp, wall);
 
					else setCell(xtemp, ytemp, floor);
				}
			}
			break;
		case 2:
		//south
			for (int ytemp = y; ytemp < (y+ylen); ytemp++){
				if (ytemp < 0 || ytemp > ysize) return false;
				for (int xtemp = (x-xlen/2); xtemp < (x+(xlen+1)/2); xtemp++){
					if (xtemp < 0 || xtemp > xsize) return false;
					if (getCell(xtemp, ytemp) != tileUnused) return false;
				}
			}
 
			for (int ytemp = y; ytemp < (y+ylen); ytemp++){
				for (int xtemp = (x-xlen/2); xtemp < (x+(xlen+1)/2); xtemp++){
 
					if (xtemp == (x-xlen/2)) setCell(xtemp, ytemp, wall);
					else if (xtemp == (x+(xlen-1)/2)) setCell(xtemp, ytemp, wall);
					else if (ytemp == y) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y+ylen-1)) setCell(xtemp, ytemp, wall);
 
					else setCell(xtemp, ytemp, floor);
				}
			}
			break;
		case 3:
		//west
			for (int ytemp = (y-ylen/2); ytemp < (y+(ylen+1)/2); ytemp++){
				if (ytemp < 0 || ytemp > ysize) return false;
				for (int xtemp = x; xtemp > (x-xlen); xtemp--){
					if (xtemp < 0 || xtemp > xsize) return false;
					if (getCell(xtemp, ytemp) != tileUnused) return false; 
				}
			}
 
			for (int ytemp = (y-ylen/2); ytemp < (y+(ylen+1)/2); ytemp++){
				for (int xtemp = x; xtemp > (x-xlen); xtemp--){
 
					if (xtemp == x) setCell(xtemp, ytemp, wall);
					else if (xtemp == (x-xlen+1)) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y-ylen/2)) setCell(xtemp, ytemp, wall);
					else if (ytemp == (y+(ylen-1)/2)) setCell(xtemp, ytemp, wall);
 
					else setCell(xtemp, ytemp, floor);
				}
			}
			break;
		}
 
		//yay, all done
		return true;
	}
 
 
	//used to print the map on the screen
	public void showDungeon(){
		for (int y = 0; y < ysize; y++){
			for (int x = 0; x < xsize; x++){
				//System.out.print(getCell(x, y));
				switch(getCell(x, y)){
				case tileUnused:
					System.out.print(" ");
					break;
				case tileDirtWall:
					System.out.print("+");
					break;
				case tileDirtFloor:
					System.out.print(".");
					break;
				case tileStoneWall:
					System.out.print("O");
					break;
				case tileCorridor:
					System.out.print("#");
					break;
				case tileDoor:
					System.out.print("D");
					break;
				case tileUpStairs:
					System.out.print("<");
					break;
				case tileDownStairs:
					System.out.print(">");
					break;
				case tileChest:
					System.out.print("*");
					break;
				};
			}
			if (xsize < xmax) System.out.println();
		}
	}
 
	//and here's the one generating the whole map
	public boolean createDungeon(int inx, int iny, int inobj){
		if (inobj < 1) objects = 10;
		else objects = inobj;
 
		//justera kartans storlek, om den ????r st????rre eller mindre ????n "gr????nserna"
		//adjust the size of the map, if it's smaller or bigger than the limits
		if (inx < 3) xsize = 3;
		else if (inx > xmax) xsize = xmax;
		else xsize = inx;
 
		if (iny < 3) ysize = 3;
		else if (iny > ymax) ysize = ymax;
		else ysize = iny;
 
		System.out.println(msgXSize + xsize);
		System.out.println(msgYSize + ysize);
		System.out.println(msgMaxObjects + objects);
 
		//redefine the map var, so it's adjusted to our new map size
		dungeon_map = new int[xsize * ysize];
 
		//start with making the "standard stuff" on the map
		for (int y = 0; y < ysize; y++){
			for (int x = 0; x < xsize; x++){
				//ie, making the borders of unwalkable walls
				if (y == 0) setCell(x, y, tileStoneWall);
				else if (y == ysize-1) setCell(x, y, tileStoneWall);
				else if (x == 0) setCell(x, y, tileStoneWall);
				else if (x == xsize-1) setCell(x, y, tileStoneWall);
 
				//and fill the rest with dirt
				else setCell(x, y, tileUnused);
			}
		}
 
		/*******************************************************************************
		And now the code of the random-map-generation-algorithm begins!
		*******************************************************************************/
 
		//start with making a room in the middle, which we can start building upon
		makeRoom(xsize/2, ysize/2, 8, 6, getRand(0,3)); //getrand saken f????r att slumpa fram riktning p?? rummet
 
		//keep count of the number of "objects" we've made
		int currentFeatures = 1; //+1 for the first room we just made
 
		//then we sart the main loop
		for (int countingTries = 0; countingTries < 1000; countingTries++){
			//check if we've reached our quota
			if (currentFeatures == objects){
				break;
			}
 
			//start with a random wall
			int newx = 0;
			int xmod = 0;
			int newy = 0;
			int ymod = 0;
			int validTile = -1;
			//1000 chances to find a suitable object (room or corridor)..
			//(yea, i know it's kinda ugly with a for-loop... -_-')
			for (int testing = 0; testing < 1000; testing++){
				newx = getRand(1, xsize-1);
				newy = getRand(1, ysize-1);
				validTile = -1;
				//System.out.println("tempx: " + newx + "\ttempy: " + newy);
				if (getCell(newx, newy) == tileDirtWall || getCell(newx, newy) == tileCorridor){
					//check if we can reach the place
					if (getCell(newx, newy+1) == tileDirtFloor || getCell(newx, newy+1) == tileCorridor){
						validTile = 0; //
						xmod = 0;
						ymod = -1;
					}
					else if (getCell(newx-1, newy) == tileDirtFloor || getCell(newx-1, newy) == tileCorridor){
						validTile = 1; //
						xmod = +1;
						ymod = 0;
					}
					else if (getCell(newx, newy-1) == tileDirtFloor || getCell(newx, newy-1) == tileCorridor){
						validTile = 2; //
						xmod = 0;
						ymod = +1;
					}
					else if (getCell(newx+1, newy) == tileDirtFloor || getCell(newx+1, newy) == tileCorridor){
						validTile = 3; //
						xmod = -1;
						ymod = 0;
					}
 
					//check that we haven't got another door nearby, so we won't get alot of openings besides
					//each other
					if (validTile > -1){
						if (getCell(newx, newy+1) == tileDoor) //north
							validTile = -1;
						else if (getCell(newx-1, newy) == tileDoor)//east
							validTile = -1;
						else if (getCell(newx, newy-1) == tileDoor)//south
							validTile = -1;
						else if (getCell(newx+1, newy) == tileDoor)//west
							validTile = -1;
					}
 
					//if we can, jump out of the loop and continue with the rest
					if (validTile > -1) break;
				}
			}
			if (validTile > -1){
				//choose what to build now at our newly found place, and at what direction
				int feature = getRand(0, 100);
				if (feature <= chanceRoom){ //a new room
					if (makeRoom((newx+xmod), (newy+ymod), 8, 6, validTile)){
						currentFeatures++; //add to our quota
 
						//then we mark the wall opening with a door
						setCell(newx, newy, tileDoor);
 
						//clean up infront of the door so we can reach it
						setCell((newx+xmod), (newy+ymod), tileDirtFloor);
					}
				}
				else if (feature >= chanceRoom){ //new corridor
					if (makeCorridor((newx+xmod), (newy+ymod), 6, validTile)){
						//same thing here, add to the quota and a door
						currentFeatures++;
 
						setCell(newx, newy, tileDoor);
					}
				}
			}
		}
 
 
		/*******************************************************************************
		All done with the building, let's finish this one off
		*******************************************************************************/
 
		//sprinkle out the bonusstuff (stairs, chests etc.) over the map
		int newx = 0;
		int newy = 0;
		int ways = 0; //from how many directions we can reach the random spot from
		int state = 0; //the state the loop is in, start with the stairs
		while (state != 10){
			for (int testing = 0; testing < 1000; testing++){
				newx = getRand(1, xsize-1);
				newy = getRand(1, ysize-2); //cheap bugfix, pulls down newy to 0<y<24, from 0<y<25
 
				//System.out.println("x: " + newx + "\ty: " + newy);
				ways = 4; //the lower the better
 
				//check if we can reach the spot
				if (getCell(newx, newy+1) == tileDirtFloor || getCell(newx, newy+1) == tileCorridor){
				//north
					if (getCell(newx, newy+1) != tileDoor)
					ways--;
				}
				if (getCell(newx-1, newy) == tileDirtFloor || getCell(newx-1, newy) == tileCorridor){
				//east
					if (getCell(newx-1, newy) != tileDoor)
					ways--;
				}
				if (getCell(newx, newy-1) == tileDirtFloor || getCell(newx, newy-1) == tileCorridor){
				//south
					if (getCell(newx, newy-1) != tileDoor)
					ways--;
				}
				if (getCell(newx+1, newy) == tileDirtFloor || getCell(newx+1, newy) == tileCorridor){
				//west
					if (getCell(newx+1, newy) != tileDoor)
					ways--;
				}
 
				if (state == 0){
					if (ways == 0){
					//we're in state 0, let's place a "upstairs" thing
						setCell(newx, newy, tileUpStairs);
						state = 1;
						break;
					}
				}
				else if (state == 1){
					if (ways == 0){
					//state 1, place a "downstairs"
						setCell(newx, newy, tileDownStairs);
						state = 10;
						break;
					}
				}
			}
		}
 
 
		//all done with the map generation, tell the user about it and finish
		System.out.println(msgNumObjects + currentFeatures);
 
		return true;
	}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
 
	public static void main(String[] args){
		//initial stuff used in making the map
		int x = 80; int y = 25; int dungeon_objects = 0;
 
		//convert a string to a int, if there's more then one arg
		if (args.length >= 1)
			dungeon_objects = Integer.parseInt(args[0]);
		if (args.length >= 2)
			x = Integer.parseInt(args[1]);
 
		if (args.length >= 3)
			y = Integer.parseInt(args[2]);
		//create a new class of "dungen", so we can use all the goodies within it
		dungen generator = new dungen();
 
		//then we create a new dungeon map
		if (generator.createDungeon(x, y, dungeon_objects)){
			//always good to be able to see the results..
			generator.showDungeon();
		}
	}
}