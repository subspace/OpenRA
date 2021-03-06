﻿#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System.Linq;
using OpenRA.Mods.RA.Activities;
using OpenRA.Effects;
using OpenRA.Traits;
using OpenRA.Traits.Activities;
using System.Drawing;

namespace OpenRA.Mods.RA
{
	class RepairableInfo : ITraitInfo, ITraitPrerequisite<HealthInfo>
	{ 
		public readonly string[] RepairBuildings = { "fix" };
		public virtual object Create(ActorInitializer init) { return new Repairable(init.self); }
	}

	class Repairable : IIssueOrder, IResolveOrder, IOrderCursor, IOrderVoice
	{
		Health Health;
		public Repairable(Actor self)
		{
			Health = self.Trait<Health>();
		}
		
		public int OrderPriority(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			return 5;
		}
		
		public Order IssueOrder(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			if (mi.Button != MouseButton.Right) return null;
			if (underCursor == null) return null;

			if (self.Info.Traits.Get<RepairableInfo>().RepairBuildings.Contains(underCursor.Info.Name)
				&& underCursor.Owner == self.Owner)
				return new Order("Repair", self, underCursor);

			return null;
		}
		
		bool CanRepair(Actor self)
		{
			var li = self.TraitOrDefault<LimitedAmmo>();
			return (Health.DamageState > DamageState.Undamaged || (li != null && !li.FullAmmo()) );
		}
		
		public string CursorForOrder(Actor self, Order order)
		{
			if (order.OrderString != "Repair") return null;
			return CanRepair(self) ? "enter" : "enter-blocked";
		}
		
		public string VoicePhraseForOrder(Actor self, Order order)
		{
			return (order.OrderString == "Repair" && CanRepair(self)) ? "Move" : null;
		}
		
		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Repair")
			{
                if (!CanRepair(self))
					return;
				
				var rp = order.TargetActor.TraitOrDefault<RallyPoint>();

				if (self.Owner == self.World.LocalPlayer)
					self.World.AddFrameEndTask(w =>
					{
						w.Add(new FlashTarget(order.TargetActor));
						var line = self.TraitOrDefault<DrawLineToTarget>();
						if (line != null)
							line.SetTarget(self, Target.FromOrder(order), Color.Green);
					});
				
				self.CancelActivity();
				self.QueueActivity(new Move(Util.CellContaining(order.TargetActor.CenterLocation), order.TargetActor));
				self.QueueActivity(new Rearm());
				self.QueueActivity(new Repair(order.TargetActor));

				if (rp != null)
					self.QueueActivity(new CallFunc(
						() => self.QueueActivity(new Move(rp.rallyPoint, order.TargetActor))));
			}
		}
	}
}
