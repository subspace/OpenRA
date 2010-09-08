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
				var cost = t.Value.NodesDict.ContainsKey("PathingCost") ? (float)FieldLoader.GetValue("cost", typeof(float), t.Value.NodesDict["PathingCost"].Value) : 1f/speed;
				ret.Add(t.Key, new TerrainInfo{Speed = speed, Cost = cost});
			}
			
			return ret;
		}
		
		public class TerrainInfo
		{
			public float Cost = float.PositiveInfinity;
			public float Speed = 0;
		}
	}
	
	public class Mobile : IIssueOrder, IResolveOrder, IOrderCursor, IOrderVoice, IOccupySpace, IMove, IFacing, INudge
	{
		public readonly Actor self;
		public readonly MobileInfo Info;
		
		[Sync]
		public int Facing { get; set; }
		[Sync]
		public int Altitude { get; set; }
		[Sync]
		public int ROT { get { return Info.ROT; } }
		public int InitialFacing { get { return Info.InitialFacing; } }
		
		int2 __fromCell, __toCell;
		public int2 fromCell
		{
			get { return __fromCell; }
			set { SetLocation( value, __toCell ); }
		}
		[Sync]
		public int2 toCell
		{
			get { return __toCell; }
			set { SetLocation( __fromCell, value ); }
		}
		void SetLocation( int2 from, int2 to )
		{
			if( fromCell == from && toCell == to ) return;
			RemoveInfluence();
			__fromCell = from;
			__toCell = to;
			AddInfluence();
		}

		Shroud shroud;
		UnitInfluence uim;
		BuildingInfluence bim;
		bool canShareCell;
		
		public Mobile(ActorInitializer init, MobileInfo info)
		{
			this.self = init.self;
			this.Info = info;
			
			shroud = self.World.WorldActor.Trait<Shroud>();
			uim = self.World.WorldActor.Trait<UnitInfluence>();
			bim = self.World.WorldActor.Trait<BuildingInfluence>();
			canShareCell = self.HasTrait<SharesCell>();
			
			if (init.Contains<LocationInit>())
			{
				this.__fromCell = this.__toCell = init.Get<LocationInit,int2>();
				AddInfluence();
				this.CenterLocation = Util.CenterOfCell( fromCell );
			}
			
			this.Facing = init.Contains<FacingInit>() ? init.Get<FacingInit,int>() : info.InitialFacing;
			this.Altitude = init.Contains<AltitudeInit>() ? init.Get<AltitudeInit,int>() : 0;
		}

		public void SetPosition(Actor self, int2 cell)
		{
			SetLocation( cell, cell );
			CenterLocation = Util.CenterOfCell(fromCell);
		}

		public Order IssueOrder(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			if (Info.OnRails) return null;
			
			if (mi.Button == MouseButton.Left) return null;

			// force-fire should *always* take precedence over move.
			if (mi.Modifiers.HasModifier(Modifiers.Ctrl)) return null;

			if (underCursor != null && underCursor.Owner != null)
			{
				// force-move
				if (!mi.Modifiers.HasModifier(Modifiers.Alt)) return null;
				if (!CanEnterCell(underCursor.Location, null, true)) return null;
			}
			if (MovementSpeedForCell(self, toCell) == 0) return null;		/* allow disabling move orders from modifiers */
			if (xy == toCell) return null;
			
			return new Order("Move", self, xy, mi.Modifiers.HasModifier(Modifiers.Shift));
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
			if (order.OrderString == "Move")
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
			if (order.OrderString != "Move")
				return null;
			
			var xy = order.TargetLocation;
			return (!shroud.exploredCells[xy.X, xy.Y] || CanEnterCell(xy)) ? "move" : "move-blocked";
		}
		
		public string VoicePhraseForOrder(Actor self, Order order)
		{
			var xy = order.TargetLocation;
			return (order.OrderString == "Move" && (!shroud.exploredCells[xy.X, xy.Y] || CanEnterCell(xy))) ? "Move" : null;
		}

		public int2 TopLeft { get { return toCell; } }
		public float2 CenterLocation { get; set; }

		public virtual IEnumerable<int2> OccupiedCells()
		{
			return (fromCell == toCell)
				? new[] { fromCell }
				: CanEnterCell(toCell)
					? new[] { toCell }
					: new[] { fromCell, toCell };
		}

		public bool CanEnterCell(int2 p)
		{
			return CanEnterCell(p, null, true);
		}

		public virtual bool CanEnterCell(int2 cell, Actor ignoreActor, bool checkTransientActors)
		{
			if (MovementCostForCell(self, cell) == float.PositiveInfinity)
				return false;

			// Check for buildings
			var building = bim.GetBuildingBlocking(cell);
			if (building != null && building != ignoreActor)
			{
				if (Info.Crushes == null)
					return false;

				var crushable = building.TraitsImplementing<ICrushable>();
				if (crushable.Count() == 0)
					return false;

				if (!crushable.Any(b => b.CrushClasses.Intersect(Info.Crushes).Any()))
					return false;
			}

			// Check mobile actors
			if (checkTransientActors && uim.AnyUnitsAt(cell))
			{
				var actors = uim.GetUnitsAt(cell).Where(a => a != self && a != ignoreActor).ToArray();
				var nonshareable = canShareCell ? actors : actors.Where(a => !a.HasTrait<SharesCell>()).ToArray();

				if (canShareCell)
				{
					var shareable = actors.Where(a => a.HasTrait<SharesCell>());

					// only allow 5 in a cell
					if (shareable.Count() >= 5)
						return false;
				}

				// We can enter a cell with nonshareable units only if we can crush all of them
				if (Info.Crushes == null && nonshareable.Length > 0)
					return false;

				if (nonshareable.Length > 0 && nonshareable.Any(a => !(a.HasTrait<ICrushable>() &&
											 a.TraitsImplementing<ICrushable>().Any(b => b.CrushClasses.Intersect(Info.Crushes).Any()))))
					return false;
			}


			return true;
		}
		
		public virtual void FinishedMoving(Actor self)
		{
			var crushable = uim.GetUnitsAt(toCell).Where(a => a != self && a.HasTrait<ICrushable>());
			foreach (var a in crushable)
			{
				var crushActions = a.TraitsImplementing<ICrushable>().Where(b => b.CrushClasses.Intersect(Info.Crushes).Any());
				foreach (var b in crushActions)
					b.OnCrush(self);
			}
		}
				
		public virtual float MovementCostForCell(Actor self, int2 cell)
		{
			if (!self.World.Map.IsInMap(cell.X,cell.Y))
				return float.PositiveInfinity;

			var type = self.World.GetTerrainType(cell);
			if (!Info.TerrainSpeeds.ContainsKey(type))
				return float.PositiveInfinity;
			
			return Info.TerrainSpeeds[type].Cost;
		}

		public virtual float MovementSpeedForCell(Actor self, int2 cell)
		{
			var type = self.World.GetTerrainType(cell);
			
			if (!Info.TerrainSpeeds.ContainsKey(type))
				return 0;
			
			var modifier = self
				.TraitsImplementing<ISpeedModifier>()
				.Select(t => t.GetSpeedModifier())
				.Product();
			return Info.Speed * Info.TerrainSpeeds[type].Speed * modifier;
		}
		
		public IEnumerable<float2> GetCurrentPath(Actor self)
		{
			var move = self.GetCurrentActivity() as Move;
			if (move == null || move.path == null) return new float2[] { };
			
			return Enumerable.Reverse(move.path).Select( c => Util.CenterOfCell(c) );
		}
		
		public virtual void AddInfluence()
		{
			uim.Add( self, this );
		}
		
		public virtual void RemoveInfluence()
		{
			uim.Remove( self, this );
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
			}
		}
	}
}
