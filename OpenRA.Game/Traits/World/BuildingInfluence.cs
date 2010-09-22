#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using OpenRA.FileFormats;
using OpenRA.GameRules;

namespace OpenRA.Traits
{
	public class BuildingInfluence
	{
		bool[,] blocked;
		Actor[,] influence;
		Map map;

		public BuildingInfluence( World world )
		{
			map = world.Map;
			
			blocked = new bool[map.MapSize.X, map.MapSize.Y];
			influence = new Actor[map.MapSize.X, map.MapSize.Y];
			
			world.ActorAdded +=
				a => { if (a.HasTrait<Building>()) 
					ChangeInfluence(a, a.Trait<Building>(), true); };
			world.ActorRemoved +=
				a => { if (a.HasTrait<Building>()) 
					ChangeInfluence(a, a.Trait<Building>(), false); };
		}

		void ChangeInfluence( Actor a, Building building, bool isAdd )
		{
			foreach( var u in Footprint.UnpathableTiles( a.Info.Traits.Get<BuildingInfo>(), a.Location ) )
				if( map.IsInMap( u ) )
					blocked[ u.X, u.Y ] = isAdd;

			foreach( var u in Footprint.Tiles( a.Info.Name, a.Info.Traits.Get<BuildingInfo>(), a.Location ) )
				if( map.IsInMap( u ) )
					influence[ u.X, u.Y ] = isAdd ? a : null;
		}

		public Actor GetBuildingAt(int2 cell)
		{
			if (!map.IsInMap(cell)) return null;
			return influence[cell.X, cell.Y];
		}
		
		public Actor GetBuildingBlocking(int2 cell)
		{
			if (!map.IsInMap(cell) || !blocked[cell.X, cell.Y]) return null;
			return influence[cell.X, cell.Y];
		}

		public bool CanMoveHere(int2 cell)
		{
			return map.IsInMap(cell) && !blocked[cell.X, cell.Y];
		}

		public bool CanMoveHere(int2 cell, Actor toIgnore)
		{
			return map.IsInMap(cell) && 
				(!blocked[cell.X, cell.Y] || influence[cell.X, cell.Y] == toIgnore);
		}
	}}
