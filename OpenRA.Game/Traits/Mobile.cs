#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Traits.Activities;
using OpenRA.FileFormats;
using System.Diagnostics;

namespace OpenRA.Traits
{
	public class MobileInfo : ITraitInfo
	{
		[FieldLoader.LoadUsing( "LoadSpeeds" )]
		public readonly Dictionary<string, TerrainInfo> TerrainSpeeds;
		[FieldLoader.Load] public readonly string[] Crushes;
		[FieldLoader.Load] public readonly int WaitAverage = 60;
		[FieldLoader.Load] public readonly int WaitSpread = 20;
		[FieldLoader.Load] public readonly int InitialFacing = 128;
		[FieldLoader.Load] public readonly int ROT = 255;
		[FieldLoader.Load] public readonly int Speed = 1;
		[FieldLoader.Load] public readonly bool OnRails = false;

		public virtual object Create(ActorInitializer init) { return new Mobile(init, this); }
		
		static object LoadSpeeds( MiniYaml y )
		{
			Dictionary<string,TerrainInfo> ret = new Dictionary<string, TerrainInfo>();
			foreach (var t in y.NodesDict["TerrainSpeeds"].Nodes)
			{
				var speed = (float)FieldLoader.GetValue("speed", typeof(float),t.Value.Value);
				var cost = t.Value.NodesDict.ContainsKey("PathingCost") ? (int)FieldLoader.GetValue("cost", typeof(int), t.Value.NodesDict["PathingCost"].Value) : (int)(10000/speed);
				ret.Add(t.Key, new TerrainInfo{Speed = speed, Cost = cost});
			}
			
			return ret;
		}
		
		public class TerrainInfo
		{
			public int Cost = int.MaxValue;
			public float Speed = 0;
		}
	}
	
	public class Mobile : IIssueOrder, IResolveOrder, IOrderCursor, IOrderVoice, IOccupySpace, IMove, IFacing, INudge
	{
		public readonly Actor self;
		public readonly MobileInfo Info;
		public bool IsMoving { get; internal set; }

		int __facing;
		int2 __fromCell, __toCell;
		int __altitude;

		[Sync]
		public int Facing
		{
			get { return __facing; }
			set
			{
				if (__facing == value)
					return;		// not interesting unless it changes 

				__facing = value; 
				Log.Write("debug", "#{0} set Facing={1}", self.ActorID, value);
				Log.Write("debug", "{0}", new StackTrace());
			}
		}

		[Sync]
		public int Altitude
		{
			get { return __altitude; }
			set { __altitude = value; Log.Write("debug", "#{0} set Altitude={1}", self.ActorID, value); }
		}
		
		public int ROT { get { return Info.ROT; } }
		public int InitialFacing { get { return Info.InitialFacing; } }

		[Sync]
		public int2 fromCell { get { return __fromCell; } }

		[Sync]
		public int2 toCell { get { return __toCell; } }

		[Sync]
		public int PathHash;	// written by Move.EvalPath, to temporarily debug this crap.

		internal void SetLocation(int2 from, int2 to)
		{
			if (fromCell == from && toCell == to) return;
			RemoveInfluence();
			__fromCell = from;
			__toCell = to;
			AddInfluence();

			Log.Write("debug", "#{0} set location = {1} {2}", self.ActorID, from, to);
			Log.Write("debug", "{0}", new StackTrace());
		}

		LocationCache locationCache;
		
		public Mobile(ActorInitializer init, MobileInfo info)
		{
			this.self = init.self;
			this.Info = info;
			
			locationCache = self.World.WorldActor.Trait<LocationCache>();
			
			if (init.Contains<LocationInit>())
			{
				this.__fromCell = this.__toCell = init.Get<LocationInit,int2>();
				AddInfluence();
			}
			
			this.Facing = init.Contains<FacingInit>() ? init.Get<FacingInit,int>() : info.InitialFacing;
			this.Altitude = init.Contains<AltitudeInit>() ? init.Get<AltitudeInit,int>() : 0;
		}

		public void SetPosition(Actor self, int2 cell)
		{
			SetLocation( cell, cell );
			self.CenterLocation = Util.CenterOfCell(fromCell);
		}

		public int OrderPriority(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			// Force move takes precedence
			return mi.Modifiers.HasModifier(Modifiers.Alt) ? int.MaxValue : 0;
		}
		
		// Note: Returns a valid order even if the unit can't move to the target
		public Order IssueOrder(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			if (Info.OnRails) return null;
			
			if (mi.Button == MouseButton.Left) return null;

			var type = (!self.World.LocalPlayer.Shroud.IsVisible(xy) || CanEnterCell(xy)) ? "Move" : "Move-Blocked";
			return new Order(type, self, xy, mi.Modifiers.HasModifier(Modifiers.Shift));
		}

		public int2 NearestMoveableCell(int2 target)
		{
			if (CanEnterCell(target))
				return target;
			
			var searched = new List<int2>(){};
			// Limit search to a radius of 10 tiles
			for (int r = 1; r < 10; r++)
				foreach (var tile in self.World.FindTilesInCircle(target,r).Except(searched))
				{
					if (CanEnterCell(tile))
						return tile;
					
					searched.Add(tile);
				}
			
			// Couldn't find a cell
			return target;
		}
		
		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Move" || order.OrderString == "Move-Blocked")
			{
				int2 currentLocation = NearestMoveableCell(order.TargetLocation);
				if (!CanEnterCell(currentLocation))
					return;
				
				if( !order.Queued ) self.CancelActivity();
				self.QueueActivity(new Activities.Move(currentLocation, 8));
				
				if (self.Owner == self.World.LocalPlayer)
					self.World.AddFrameEndTask(w =>
					{
						w.Add(new MoveFlash(self.World, order.TargetLocation));
						var line = self.TraitOrDefault<DrawLineToTarget>();
						if (line != null)
							line.SetTarget(self, Target.FromCell(currentLocation), Color.Green);
					});
			}
		}
		
		public string CursorForOrder(Actor self, Order order)
		{
			if (order.OrderString == "Move")
				return "move";
			
			if (order.OrderString == "Move-Blocked")
				return "move-blocked";
			
			return null;
		}
		
		public string VoicePhraseForOrder(Actor self, Order order)
		{
			if (order.OrderString == "Move" || order.OrderString == "Move-Blocked")
				return "Move";
			return null;
		}

		public int2 TopLeft { get { return toCell; } }

		public IEnumerable<int2> OccupiedCells()
		{
			return (fromCell == toCell)
				? new[] { fromCell }
				: CanEnterCell(toCell)
					? new[] { toCell }
					: new[] { fromCell, toCell };
		}

		public bool CanEnterCell(int2 cell)
		{
			return CanEnterCell( self.World, Info, cell, null, true );
		}

		public static bool CanEnterCell( World world, MobileInfo mi, int2 cell, Actor ignoreActor, bool checkTransientActors )
		{
			var lc = world.WorldActor.Trait<LocationCache>();
			return Mobile.CanEnterCell( world, mi, lc, cell, ignoreActor, checkTransientActors );
		}
		
		public static bool CanEnterCell( World world, MobileInfo mobileInfo, LocationCache lc, int2 cell, Actor ignoreActor, bool checkTransientActors )
		{
			if (MovementCostForCell(mobileInfo, world, cell) == int.MaxValue)
				return false;

			if (!checkTransientActors)
				return true;

			var blockers = lc.ActorsBlocking( cell ).Where( x => x != ignoreActor ).ToList();
			if (blockers.Count > 0)
			{
				// We can enter a cell with nonshareable units only if we can crush all of them
				if (mobileInfo.Crushes == null)
					return false;

				if (blockers.Any(a => !(a.HasTrait<ICrushable>() &&
											 a.TraitsImplementing<ICrushable>().Any(b => b.CrushClasses.Intersect(mobileInfo.Crushes).Any()))))
					return false;
			}

			return true;
		}
		
		public void FinishedMoving(Actor self)
		{
			var crushable = locationCache.uim.GetUnitsAt(toCell).Where(a => a != self && a.HasTrait<ICrushable>());
			foreach (var a in crushable)
			{
				var crushActions = a.TraitsImplementing<ICrushable>().Where(b => b.CrushClasses.Intersect(Info.Crushes).Any());
				foreach (var b in crushActions)
					b.OnCrush(self);
			}
		}

		public static int MovementCostForCell(MobileInfo info, World world, int2 cell)
		{
			if (!world.Map.IsInMap(cell.X,cell.Y))
				return int.MaxValue;

			var type = world.GetTerrainType(cell);
			if (!info.TerrainSpeeds.ContainsKey(type))
				return int.MaxValue;
			
			return info.TerrainSpeeds[type].Cost;
		}

		public float MovementSpeedForCell(Actor self, int2 cell)
		{
			var type = self.World.GetTerrainType(cell);
			
			if (!Info.TerrainSpeeds.ContainsKey(type))
				return 0;
			
			var modifier = self
				.TraitsImplementing<ISpeedModifier>()
				.Select(t => t.GetSpeedModifier())
				.Product();
			return Info.Speed * Info.TerrainSpeeds[type].Speed * modifier / 100f;
		}
		
		public void AddInfluence()
		{
			locationCache.uim.Add( self, this );
		}
		
		public void RemoveInfluence()
		{
			locationCache.uim.Remove( self, this );
		}

		public void OnNudge(Actor self, Actor nudger)
		{
			/* initial fairly braindead implementation. */

			if (self.Owner.Stances[nudger.Owner] != Stance.Ally)
				return;		/* don't allow ourselves to be pushed around
							 * by the enemy! */

			if (!self.IsIdle)
				return;		/* don't nudge if we're busy doing something! */

			// pick an adjacent available cell.
			var availCells = new List<int2>();
			var notStupidCells = new List<int2>();

			for( var i = -1; i < 2; i++ )
				for (var j = -1; j < 2; j++)
				{
					var p = toCell + new int2(i, j);
					if (CanEnterCell(p))
						availCells.Add(p);
					else
						if (p != nudger.Location && p != toCell)
							notStupidCells.Add(p);
				}

			var moveTo = availCells.Any() ? availCells.Random(self.World.SharedRandom) :
				notStupidCells.Any() ? notStupidCells.Random(self.World.SharedRandom) : (int2?)null;

			if (moveTo.HasValue)
			{
				self.CancelActivity();
				if (self.Owner == self.World.LocalPlayer)
					self.World.AddFrameEndTask(w =>
					{
						var line = self.TraitOrDefault<DrawLineToTarget>();
						if (line != null)
							line.SetTargetSilently(self, Target.FromCell(moveTo.Value), Color.Green);
					});
				self.QueueActivity(new Move(moveTo.Value, 0));

				Log.Write("debug", "OnNudge #{0} from {1} to {2}",
					self.ActorID, self.Location, moveTo.Value);
			}
			else
				Log.Write("debug", "OnNudge #{0} refuses at {1}",
					self.ActorID, self.Location);
		}
	}
}
