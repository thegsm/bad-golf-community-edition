﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class networkManagerServer : MonoBehaviour {
	Dictionary<float,string> screenMessages = new Dictionary<float,string>();
	networkVariables nvs;
	PlayerInfo myInfo;
	string serverVersion;
	
	/****************************************************
	 * 
	 * DONT EDIT THIS SCRIPT UNLESS ITS TO ADD ANYTHING
	 * IN THE "ANY SERVER SIDE SCRIPTS GO HERE" SECTION
	 * 
	 ****************************************************/
	
	// Use this for initialization
	void Start () {
		// setup reference to networkVariables
		nvs = gameObject.GetComponent("networkVariables") as networkVariables;
		myInfo = nvs.myInfo;
		
		// add us to the player list
		nvs.players.Add(myInfo);
		
		// get server version
		serverVersion = nvs.serverVersion;
		// get server name
		string serverName = nvs.serverName;
		
		// Use NAT punchthrough if no public IP present
		Network.InitializeServer(31, 11177, !Network.HavePublicAddress());
		MasterServer.RegisterHost(serverVersion, SystemInfo.deviceName, serverName);

		gameObject.AddComponent("netLobby");
	}
	
	// ANY SERVER SIDE SCRIPTS GO HERE
	void AddScripts() {
		// receives all players inputs and handles fiziks
		gameObject.AddComponent("controlServer");
		
		// chat
		gameObject.AddComponent("netChat");
		
		//pause
		gameObject.AddComponent ("netPause");
		
		//cart reset
		gameObject.AddComponent ("netPlayerRespawn");
		
		// set the camera in the audio script on the buggy - PUT THIS IN A SCRIPT SOMEONE
		CarAudio mca = myInfo.cartGameObject.GetComponent("CarAudio") as CarAudio;
		mca.followCamera = nvs.myCam;	// replace tmpCam with our one - this messes up sound atm
		(nvs.myCam.gameObject.AddComponent("FollowPlayerScript") as FollowPlayerScript).target = myInfo.cartGameObject.transform;	// add player follow script
		
		// add the swing script
		//gameObject.AddComponent("netSwing");
	}

	// carts for all!
	void BeginGame() {
		Vector3 velocity = new Vector3(0,0,0);
		float i = 0;
		float spacer = 360 / nvs.players.Count;
		foreach (PlayerInfo newGuy in nvs.players) {
			// create new buggy for the new guy - his must be done on the server otherwise collisions wont work!
			Vector3 spawnLocation = transform.position + Quaternion.AngleAxis(spacer * i++, Vector3.up) * new Vector3(10,2,0);
			
			// instantiate the prefabs
			GameObject cartGameObject = Instantiate(Resources.Load(newGuy.cartModel), spawnLocation, Quaternion.identity) as GameObject;
			GameObject ballGameObject = Instantiate(Resources.Load(newGuy.ballModel), spawnLocation + new Vector3(3,0,0), Quaternion.identity) as GameObject;
			GameObject characterGameObject = Instantiate(Resources.Load(newGuy.characterModel), spawnLocation, Quaternion.identity) as GameObject;
			// set buggy as characters parent
			characterGameObject.transform.parent = cartGameObject.transform;
			
			// create and set viewIDs
			NetworkViewID cartViewIDTransform = Network.AllocateViewID();					// track the transform of the cart
			NetworkView cgt = cartGameObject.GetComponent("NetworkView") as NetworkView;
			cgt.observed = cartGameObject.transform;
			cgt.viewID = cartViewIDTransform;
			cgt.stateSynchronization = NetworkStateSynchronization.Unreliable;
			NetworkViewID cartViewIDRigidbody = Network.AllocateViewID();					// track the rigidbody of the cart
			NetworkView cgr = cartGameObject.AddComponent("NetworkView") as NetworkView;
			cgr.observed = cartGameObject.rigidbody;
			cgr.viewID = cartViewIDRigidbody;
			cgr.stateSynchronization = NetworkStateSynchronization.Unreliable;
			NetworkViewID ballViewID = Network.AllocateViewID();
			ballGameObject.networkView.viewID = ballViewID;
			NetworkViewID characterViewID = Network.AllocateViewID();
			characterGameObject.networkView.viewID = characterViewID;
			
			// edit their PlayerInfo
			newGuy.cartGameObject = cartGameObject;
			newGuy.cartViewIDTransform = cartViewIDTransform;
			newGuy.cartViewIDRigidbody = cartViewIDRigidbody;
			newGuy.ballGameObject = ballGameObject;
			newGuy.ballViewID = ballViewID;
			newGuy.characterGameObject = characterGameObject;
			newGuy.characterViewID = characterViewID;
			newGuy.currentMode = 0;	// set them in buggy

			// tell everyone else about it
			networkView.RPC("SpawnPrefab", RPCMode.Others, cartViewIDTransform, spawnLocation, velocity, newGuy.cartModel);
			networkView.RPC("SpawnPrefab", RPCMode.Others, ballViewID, spawnLocation, velocity, newGuy.ballModel);
			networkView.RPC("SpawnPrefab", RPCMode.Others, characterViewID, spawnLocation, velocity, newGuy.characterModel);
			
			// tell all players this is a player and not some random objects
			networkView.RPC("SpawnPlayer", RPCMode.Others, cartViewIDTransform, cartViewIDRigidbody, ballViewID, characterViewID, 0, newGuy.player);

			if (newGuy.player!=myInfo.player) {
				// tell the player it's theirs
				networkView.RPC("ThisOnesYours", newGuy.player, cartViewIDTransform, ballViewID, characterViewID);
			}
		}
	}
	
	// fired when a player joins (if you couldn't tell)
	void OnPlayerConnected(NetworkPlayer player) {
		networkView.RPC("PrintText", player, "Welcome to the test server");
		PrintText("Someone joined");
		
		// add them to the list
		PlayerInfo newGuy = new PlayerInfo();
		newGuy.player = player;
		newGuy.name = "Some guy";
		newGuy.cartModel = nvs.buggyModels[0];
		newGuy.ballModel = nvs.ballModels[0];
		newGuy.characterModel = nvs.characterModels[0];
		
		// send all current players to new guy
		foreach (PlayerInfo p in nvs.players)
		{
			/* goodbye old code, you will be missed
			networkView.RPC("SpawnPrefab", player, p.cartViewIDTransform, p.cartGameObject.transform.position, new Vector3(0,0,0), p.cartModel);
			networkView.RPC("SpawnPrefab", player, p.ballViewID, p.ballGameObject.transform.position, new Vector3(0,0,0), p.ballModel);
			networkView.RPC("SpawnPrefab", player, p.characterViewID, p.characterGameObject.transform.position, new Vector3(0,0,0), p.characterModel);
			// tell the player this is a player and not some random objects
			networkView.RPC("SpawnPlayer", player, p.cartViewIDTransform, p.cartViewIDRigidbody, p.ballViewID, p.characterViewID, p.currentMode, p.player);
			*/
			// tell the new player about the iterated player
			networkView.RPC("AddPlayer", player, p.cartModel, p.ballModel, p.characterModel, p.player, p.name);
			// tell the iterated player about the new player, unless the iterated player is the server
			if (p.player!=myInfo.player) {
				networkView.RPC("AddPlayer", p.player, newGuy.cartModel, newGuy.ballModel, newGuy.characterModel, newGuy.player, newGuy.name);
			}
		}
		
		// add it to the list
		nvs.players.Add(newGuy);
	}

	void OnPlayerDisconnected(NetworkPlayer player) {
		// tell all players to remove them
		networkView.RPC("RemovePlayer", RPCMode.All, player);
		
		// remove all their stuff
		Network.RemoveRPCs(player);
		Network.DestroyPlayerObjects(player);
		
		PlayerInfo toDelete = new PlayerInfo();
		foreach (PlayerInfo p in nvs.players)
		{
			if (p.player==player) {
				if (p.currentMode==0 || p.currentMode==1) {
					// remove their stuff
					Destroy(p.cartGameObject);
					Destroy(p.ballGameObject);
					Destroy(p.characterGameObject);
					// tell everyone else to aswell - move this onto the server
					networkView.RPC("RemoveViewID", RPCMode.All, p.characterViewID);
					networkView.RPC("RemoveViewID", RPCMode.All, p.cartViewIDTransform);
					networkView.RPC("RemoveViewID", RPCMode.All, p.ballViewID);

				} else if (p.currentMode==2) {// if they haven't got anything yet
				}
				// remove from array
				toDelete = p;
			}
		}
		if (nvs.players.Contains(toDelete)) nvs.players.Remove(toDelete);
	}
	
	// debug shit
	void OnGUI() {
		GUILayout.BeginHorizontal();
		GUILayout.Label ("Active Players: ");
		GUILayout.Label (nvs.players.Count.ToString());
		GUILayout.EndHorizontal();
		
		float keyToRemove = 0;
		// show any debug messages
		foreach (KeyValuePair<float,string> msgs in screenMessages) {
			if (msgs.Key < Time.time) {
				keyToRemove = msgs.Key;	// don't worry about there being more than 1 - it'll update next frame
			} else {
				GUILayout.BeginHorizontal();
				GUILayout.Label(msgs.Value);
				GUILayout.EndHorizontal();
			}
		}
		if (screenMessages.ContainsKey(keyToRemove)) screenMessages.Remove(keyToRemove);
	}
	
	void OnDisconnectedFromServer(NetworkDisconnection info){
		MasterServer.UnregisterHost();
		
		//Go back to main menu
		string nameOfLevel = "main";
		Application.LoadLevel( nameOfLevel );
	}

	void StartGame() {
		Component.Destroy(GetComponent("netLobby"));

		// don't let anyone else join - this doesn't work (and hasn't since 2010 -_-)
		MasterServer.UnregisterHost();
		// instead make up a password and set it to that
		string tmpPwCuzUnitysShit = "";
		for (int i=0; i<10; i++) {
			tmpPwCuzUnitysShit += (char)(Random.Range(65,90));
		}
		Debug.Log(tmpPwCuzUnitysShit);
		Network.incomingPassword = tmpPwCuzUnitysShit;

		// tell everyone what their choices were
		foreach (PlayerInfo p in nvs.players)
		{
			if (p.player!=myInfo.player) {
				networkView.RPC("StartingGame", p.player, p.cartModel, p.ballModel, p.characterModel);
			}
		}

		// start the game
		BeginGame();
		// call the functions that need them
		AddScripts();
	}
	
	
	
	
	// things that can be run over the network
	// debug text
	[RPC]
	void PrintText(string text) {
		Debug.Log(text);
		screenMessages.Add(Time.time+5,"[DEBUG] "+text);
	}

	[RPC]
	void ChangeModels(string cartModel, string ballModel, string characterModel, NetworkMessageInfo info) {
		foreach (PlayerInfo p in nvs.players) {
			if (p.player==info.sender) {
				p.cartModel = cartModel;
				p.ballModel = ballModel;
				p.characterModel = characterModel;
				// add something that updates clients
			}
		}
	}

	[RPC]
	void MyName(string name, NetworkMessageInfo info){
		foreach(PlayerInfo p in nvs.players) {
			if (p.player==info.sender) {
				p.name = name;
				// add something that updates clients
			}
		}
	}
	
	// blank for client use only
	[RPC]
	void ThisOnesYours(NetworkViewID viewID, NetworkViewID b, NetworkViewID c) {}
	[RPC]
	void SpawnPrefab(NetworkViewID viewID, Vector3 spawnLocation, Vector3 velocity, string prefabName) {}
	[RPC]
	void SpawnPlayer(NetworkViewID viewID, NetworkViewID b, NetworkViewID c, NetworkViewID d, int mode, NetworkPlayer p) {}
	[RPC]
	void RemoveViewID(NetworkViewID viewID) {}
	[RPC]
	void RemovePlayer(NetworkPlayer p) {}
	[RPC]
	void StartingGame(string a, string b, string c) {}
	[RPC]
	void AddPlayer(string cartModel, string ballModel, string characterModel, NetworkPlayer player, string name) {}
}
