//Automated Patrol and Defence drone
//Created by: Eldar Storm
//Website: http://radioactivecricket.com
//GitHub: https://github.com/eldarstorm/SE_Defence_and_Patrol_Drone

const string strSensor = "SENSOR"; //Name your Sensor this
const string strModeIndicator = "INDICATOR";  //Interior Light - Mode Indicator (Optional)
const string strPatrol = "PATROL"; //For Second RC Block used for Patrol (Optional)
const bool patrolOveride = false; //Set true if you want to overide the Patrol autodetection. true = no patrol

IMySensorBlock sensor = null;
IMyCubeGrid targetGrid = null;
IMyInteriorLight modeIndicator = null;
IMyRemoteControl patrol = null;
Vector3D lastLoc = new Vector3D(0, 0, 0);
Vector3D blankLoc = new Vector3D(0, 0, 0);

bool patrolEnabled = false; //Code automatically sets this

List<IMyTerminalBlock> list = new List<IMyTerminalBlock>();

//0 = idle
//1 = patrol
//2 = return home
//3 = follow
//4 = Return LastLoc
int mode = 0;

void Main(string argument)
{
	string status = "";

	Vector3D origin = new Vector3D(0, 0, 0);
    if (argument == null || argument == "")
    {
        origin = Me.GetPosition();
        Me.TerminalRunArgument = origin.ToString();
    }
    else
    {
        Vector3D.TryParse(argument, out origin);
    }
	
	if(!patrolOveride)
		patrol = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName(strPatrol);
	
	if( patrol == null)
		patrolEnabled = false;
	else
		patrolEnabled = true;

	GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(list);
	if(list.Count > 0)
	{
		status = "RC Block Found \n";
		
		var remote = list[0] as IMyRemoteControl;
		if(list.Count > 1 && patrol != null)
		{
			if(patrol == remote)
				remote = list[1] as IMyRemoteControl;
		}
		
		remote.ClearWaypoints();
		Vector3D target = new Vector3D(0,0,0);
		
		if(patrolEnabled)
			status = status+"Patrol System Enabled \n";
		else
			status = status+"Patrol System Dissabled \n";

		sensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(strSensor);
		modeIndicator = (IMyInteriorLight)GridTerminalSystem.GetBlockWithName(strModeIndicator);
		if (sensor != null)
		{
			bool sensorExist = (sensor != null && sensor.IsFunctional);
			IMyCubeGrid detectedGrid = null;

			if(sensorExist)
			{
				status = status+"Sensor Found \n\n";

				if (targetGrid == null)
				{
					try
					{
						detectedGrid = sensor.LastDetectedEntity as IMyCubeGrid;
					}
					catch (Exception)
					{
						detectedGrid = null;
						targetGrid = null;
					}

					if (detectedGrid != null)
					{
						bool gridCheck = validTarget(detectedGrid);
						if(gridCheck)
						{
							status = status+"Found: "+detectedGrid.ToString()+"\n";
							targetGrid = detectedGrid;
							status = status+targetGrid.GetPosition().ToString()+"\n";
							target = targetGrid.GetPosition();
						}
						else
						{
							status = status+"No Target Grid Found. \n";
							if(mode == 3 && mode != 4)
								mode = 2;
						}
					}
					else
					{
						status = status+"No Target Grid Found. \n";
						if(mode == 3 && mode != 4)
							mode = 2;
					}
				}
				else
				{

					bool gridCheck = validTarget(targetGrid);
					if(gridCheck)
					{
						target = targetGrid.GetPosition();
						status = status+"Target: "+targetGrid.ToString()+"\n";
						status = status+"Pos: "+target.ToString()+"\n";
						mode = 3;
						
						if(patrolEnabled)
							lastLoc = Me.GetPosition();
					}
					else
					{
						if(lastLoc != blankLoc)
							mode = 4;
						else
							mode = 2;
						
						status = status+"Target Lost \n";
						targetGrid = null;
					}
				}
			}
			else
			{
				status = status+"Sensor Damaged or Destroyed\n\n";
			}
		}
		else
		{
			status = status+"Sensor Not Found\n\n";
			mode = 2;
		}

		GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(list);
		if (list.Count == 0)
		{
			mode = 2;
		}

		if (Vector3D.DistanceSquared(target, origin) > 20000 * 20000)
		{
			if(patrolEnabled)
				mode = 4;
			else
				mode = 2;
		}
		
		switch (mode)
		{
			case 0:	//Idle
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(1f, 1f, 0f));
				
				stopPatrol();
				
				status = status+"\n\nStatus: "+"Idle";
				remote.SetAutoPilotEnabled(false);
				remote.ClearWaypoints();
				DissableTurrets();
				lastLoc = new Vector3D(0, 0, 0);
				
				if(patrolEnabled)
					mode = 1;
				break;
			case 1: //Patrol
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(0f, 0.5f, 1f));
				
				if(patrol != null)
					patrol.SetAutoPilotEnabled(true);
				else
				{
					patrolEnabled = false;
					mode = 0;
				}
				
				status = status+"\n\nStatus: "+"Patrol";
				DissableTurrets();
				break;
			case 2: //Go Home
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(0f, 1f, 0f));
				
				stopPatrol();
				
				remote.AddWaypoint(origin, "Origin");
				status = status+"\n\nStatus: "+"Returning Home";
				remote.SetAutoPilotEnabled(true);
				DissableTurrets();
				
				if(gridAtLoc(Me.CubeGrid, origin))
				{
					mode = 0;
					remote.ClearWaypoints();
				}

				break;
			case 3: //Follow
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(1f, 0f, 0f));
				
				stopPatrol();
				
				remote.AddWaypoint(target, "Target");
				status = status+"\n\nStatus: "+"Following Target";
				remote.SetAutoPilotEnabled(true);
				EnableTurrets();
				break;
			case 4: //Return Last Location
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(0.5f, 1f, 0.5f));
				
				stopPatrol();
				
				if(patrolEnabled && lastLoc != blankLoc)
				{
					remote.AddWaypoint(lastLoc, "Last Location");
					remote.SetAutoPilotEnabled(true);
					
					if(gridAtLoc(Me.CubeGrid, lastLoc))
					{
						mode = 0;
						remote.ClearWaypoints();
					}
				}
				else
					mode = 2;
				break;
			default:
				if (modeIndicator != null)
					modeIndicator.SetValue<Color>("Color", new Color(1f, 1f, 0f));
				
				stopPatrol();
				
				mode = 0;
				status = status+"\n\nStatus: "+"Default goto Idle";
				remote.SetAutoPilotEnabled(false);
				DissableTurrets();
				break;
		}

	}
	else
	{
		status = "Unable to Find RC \n";
		mode = 0;
	}

	var lcd = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;
	if (lcd != null)
	{
		lcd.WritePublicText(status);
		lcd.ShowTextureOnScreen();
		lcd.ShowPublicTextOnScreen();
	}
}

//If Patrol RC then it will dissable autopilot when called
void stopPatrol()
{
	if(patrol != null)
		patrol.SetAutoPilotEnabled(false);
}

//Enable all turrets on the grid
void EnableTurrets()
{
	GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(list);
	if (list.Count > 0)
	{
		for (int i = 0; i < list.Count; i += 1)
		{
			list[i].GetActionWithName("OnOff_On").Apply(list[i]);
		}
	}
}

//Dissable all turrets on the grid
void DissableTurrets()
{
	GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(list);
	if (list.Count > 0)
	{
		for (int i = 0; i < list.Count; i += 1)
		{
			list[i].GetActionWithName("OnOff_Off").Apply(list[i]);
		}
	}
}

//Returns the Vector3I location of a grid
Vector3I getV3IPos(IMyCubeGrid grid)
{
    int x1 = grid.Min.AxisValue(Base6Directions.Axis.LeftRight);
    int y1 = grid.Min.AxisValue(Base6Directions.Axis.UpDown);
    int z1 = grid.Min.AxisValue(Base6Directions.Axis.ForwardBackward);
    int x2 = grid.Max.AxisValue(Base6Directions.Axis.LeftRight);
    int y2 = grid.Max.AxisValue(Base6Directions.Axis.UpDown);
    int z2 = grid.Max.AxisValue(Base6Directions.Axis.ForwardBackward);
    int xMid = (x1 + x2) / 2;
    int yMid = (y1 + y2) / 2;
    int zMid = (z1 + z2) / 2;

	return new Vector3I(xMid, yMid, zMid);
}

//Checks to see if the given grid is at the given location
bool gridAtLoc(IMyCubeGrid grid, Vector3D location)
{
	Vector3I locationV3I = grid.WorldToGridInteger(location);
	bool gridCheck = grid.CubeExists(locationV3I);
	
	return gridCheck;
}

//Makes sure the target is still valid
bool validTarget(IMyCubeGrid grid)
{
	Vector3I targetV3I = getV3IPos(grid);
	bool gridCheck = grid.CubeExists(targetV3I);
	if(gridCheck)
	{
		//Checks to see if the block was destroyed
		IMySlimBlock slimBlock = grid.GetCubeBlock(targetV3I);
		if(slimBlock == null)
			return true;
		else
		{
			if(slimBlock.IsDestroyed)
			{
				return false;
			}
			else
			{
				return true;
			}
		}
	}

	return false;
}
