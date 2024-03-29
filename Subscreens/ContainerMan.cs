using System;
using System.Collections.Generic;

namespace Noxico
{
	/// <summary>
	/// Displays the player's inventory and a container's contents to trade items.
	/// </summary>
	public class ContainerMan
	{
		private enum ContainerMode
		{
			Chest, Vendor, Corpse
		}

		private static List<Token> containerTokens = new List<Token>();
		private static List<InventoryItem> containerItems = new List<InventoryItem>();

		private static List<Token> playerTokens = new List<Token>();
		private static List<InventoryItem> playerItems = new List<InventoryItem>();

		private static ContainerMode mode;
		private static Token other;
		private static string title;
		private static Character vendorChar;
		private static float price;
		private static bool vendorCaughtYou;

		private static UIWindow containerWindow, playerWindow, descriptionWindow;
		private static UIList containerList, playerList;
		private static UILabel description;
		private static UILabel capacity;
		private static int indexLeft, indexRight;

		/// <summary>
		/// Sets up a ContainerMan subscreen for actual containers, such as chests and corpses.
		/// </summary>
		/// <param name="container">The Container to display on the left.</param>
		public static void Setup(Container container)
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = ContainerMan.Handler;
			Subscreens.FirstDraw = true;

			other = container.Token.GetToken("contents");
			title = container.Name;
			mode = container.Token.HasToken("corpse") ? ContainerMode.Corpse : ContainerMode.Chest;
			//We're not doing business, so clear out the vendor.
			vendorChar = null;
			vendorCaughtYou = false;
		}

		/// <summary>
		/// Sets up a ContainerMan subscreen for trading with a vendor.
		/// </summary>
		/// <param name="vendor">The Character whose items to display on the left.</param>
		public static void Setup(Character vendor)
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = ContainerMan.Handler;
			Subscreens.FirstDraw = true;

			other = vendor.GetToken("items");
			title = vendor.Name.ToString(true);
			mode = ContainerMode.Vendor;
			vendorChar = vendor;
			vendorCaughtYou = false;
		}

		/// <summary>
		/// Generic Subscreen handler.
		/// </summary>
		public static void Handler()
		{
			var keys = NoxicoGame.KeyMap;
			var player = NoxicoGame.Me.Player;
			var width = (Program.Cols / 2) - 2;

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				UIManager.Elements.Clear();

				//Prepare the left item list and its UIElements...
				var height = 1;
				containerTokens.Clear();
				containerItems.Clear();
				containerList = null;
				var containerTexts = new List<string>();

				containerWindow = new UIWindow(title) { Left = 1, Top = 1, Width = width, Height = 2 + height };
				containerList = new UIList(string.Empty, null, containerTexts) { Width = width - 2, Height = height, Index = indexLeft, Background = UIColors.WindowBackground };
				containerList.Move(1, 1, containerWindow);
				UIManager.Elements.Add(containerWindow);
				var emptyMessage = mode == ContainerMode.Vendor ? i18n.Format("inventory_x_hasnothing", vendorChar.Name.ToString()) : mode == ContainerMode.Corpse ? i18n.GetString("inventory_nothingleft") : i18n.GetString("inventory_empty");
				var emptyMessageC = new UILabel(emptyMessage) { Width = width - 3, Height = 1 };
				emptyMessageC.Move(2, 1, containerWindow);
				UIManager.Elements.Add(emptyMessageC);
				UIManager.Elements.Add(containerList);

				if (other.Tokens.Count == 0)
				{
					containerList.Hidden = true;
					containerWindow.Height = 3;
				}
				else
				{
					//Populate the left list...
					foreach (var carriedItem in other.Tokens)
					{
						//Only let vendors sell things meant for sale, not their literal shirt off their back
						if (vendorChar != null && !carriedItem.HasToken("for_sale"))
							continue;

						var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
						if (find == null)
							continue;
						containerTokens.Add(carriedItem);
						containerItems.Add(find);

						var item = find;
						var itemString = item.ToString(carriedItem, false, false);
						if (itemString.Length > width - 5)
							itemString = itemString.Disemvowel();
						itemString = itemString.PadEffective(width - 5);
						//Add any extra sigils.
						if (mode == ContainerMode.Vendor && carriedItem.HasToken("equipped"))
							itemString = itemString.Remove(width - 6) + i18n.GetString("sigil_short_worn");
						if (carriedItem.Path("cursed/known") != null)
							itemString = itemString.Remove(width - 6) + "C"; //DO NOT TRANSLATE -- Curses will be replaced with better terms and variants such as "Slippery" or "Sticky".
						containerTexts.Add(itemString);
					}
					height = containerItems.Count;
					if (height > Program.Rows - 15)
						height = Program.Rows - 15;
					if (indexLeft >= containerItems.Count)
						indexLeft = containerItems.Count - 1;

					containerList.Items.Clear();
					containerList.Items.AddRange(containerTexts);
					containerWindow.Height = 2 + height;
					containerList.Height = height;
				}

				//Prepare the right item list and its UIElements...
				playerTokens.Clear();
				playerItems.Clear();
				playerList = null;
				var playerTexts = new List<string>();

				playerWindow = new UIWindow(i18n.GetString("inventory_yours")) { Width = width, Height = 3 };
				playerWindow.MoveBeside(2, 0, containerWindow);
				playerList = new UIList(string.Empty, null, playerTexts) { Width = width - 2, Height = 1, Index = indexRight, Background = UIColors.WindowBackground };
				playerList.Move(1, 1, playerWindow);
				UIManager.Elements.Add(playerWindow);
				var playerNothing = new UILabel(i18n.GetString("inventory_youhavenothing")) { Left = width + 5, Top = 2, Width = width - 3, Height = 1 };
				playerNothing.Move(2, 1, playerWindow);
				UIManager.Elements.Add(playerNothing);
				UIManager.Elements.Add(playerList);

				if (player.Character.GetToken("items").Tokens.Count == 0)
				{
					playerList.Hidden = true;
					playerWindow.Height = 3;
				}
				else
				{
					//Populate the right list...
					foreach (var carriedItem in player.Character.GetToken("items").Tokens)
					{
						var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
						if (find == null)
							continue;
						playerTokens.Add(carriedItem);
						playerItems.Add(find);

						var item = find;
						var itemString = item.ToString(carriedItem, false, false);
						if (itemString.Length > width - 5)
							itemString = itemString.Disemvowel();
						itemString = itemString.PadEffective(width - 5);
						if (carriedItem.HasToken("equipped"))
							itemString = itemString.Remove(width - 6) + i18n.GetString("sigil_short_worn");
						if (carriedItem.Path("cursed/known") != null)
							itemString = itemString.Remove(width - 6) + "C"; //DO NOT TRANSLATE -- Curses will be replaced with better terms and variants such as "Slippery" or "Sticky".
						playerTexts.Add(itemString); 
					}
					var height2 = playerItems.Count;
					if (height2 == 0)
						height2 = 1;
					if (height2 > Program.Rows - 15)
						height2 = Program.Rows - 15;
					if (indexRight >= playerItems.Count)
						indexRight = playerItems.Count - 1;

					if (height2 > height)
						height = height2;

					playerList.Items.Clear();
					playerList.Items.AddRange(playerTexts);
					playerWindow.Height = 2 + height2;
					playerList.Height = height2;
				}

				//Build the bottom window.
				UIManager.Elements.Add(new UILabel(new string(' ', Program.Cols)) { Left = 0, Top = 0, Width = Program.Cols - 1, Height = 1, Background = UIColors.StatusBackground, Foreground = UIColors.StatusForeground });
				UIManager.Elements.Add(new UILabel(i18n.GetString(mode == ContainerMode.Vendor ? "inventory_pressenter_vendor" : "inventory_pressenter_container")) { Left = 0, Top = 0, Width = Program.Cols - 1, Height = 1, Background = UIColors.StatusBackground, Foreground = UIColors.StatusForeground });
				descriptionWindow = new UIWindow(string.Empty) { Left = 2, Top = Program.Rows - 10, Width = Program.Cols - 4, Height = 8, Title = UIColors.RegularText };
				description = new UILabel(string.Empty) { Width = Program.Cols - 8, Height = 5 };
				description.Move(2, 1, descriptionWindow);
				capacity = new UILabel(string.Format("{0:F2}/{1:F2}", player.Character.Carried, player.Character.Capacity));
				capacity.MoveBelow(4, -1, descriptionWindow);
				UIManager.Elements.Add(descriptionWindow);
				UIManager.Elements.Add(description);
				UIManager.Elements.Add(capacity);
				//Should we be trading, replace the weight with a money indication.
				if (mode == ContainerMode.Vendor)
					capacity.Text = i18n.Format("inventory_money", vendorChar.Name.ToString(), vendorChar.GetToken("money").Value, player.Character.GetToken("money").Value);

				//FIXME: why is this check a thing?
				if (containerList != null)
				{
					containerList.Change = (s, e) =>
					{
						if (containerList.Items.Count == 0)
							return;
						indexLeft = containerList.Index;
						//Build up the content for the description window.
						var t = containerTokens[containerList.Index];
						var i = containerItems[containerList.Index];
						descriptionWindow.Text = i.ToString(t, false, false);
						var desc = i.GetDescription(t);
						price = 0; //Reset price no matter the mode so we don't accidentally pay for free shit.
						if (mode == ContainerMode.Vendor && i.HasToken("price"))
						{
							price = i.GetToken("price").Value;
							desc += "\n" + i18n.Format("inventory_itcosts", price);
						}
						if (mode == ContainerMode.Vendor && i.HasToken("equipable") && t.HasToken("equipped"))
							desc += "\n" + i18n.Format("inventory_vendorusesthis", vendorChar.Name.ToString());
						description.Text = Toolkit.Wordwrap(desc.SmartQuote(), description.Width);
						descriptionWindow.Draw();
						description.Draw();
						capacity.Draw();
					};
					containerList.Enter = (s, e) =>
					{
						if (containerList.Items.Count == 0)
							return; 
						//onLeft = true;
						var tryAttempt = TryRetrieve(player, containerTokens[containerList.Index], containerItems[containerList.Index]);
						if (tryAttempt.IsBlank())
						{
							//No errors were returned by TryRetrieve, so let's do this.
							playerItems.Add(containerItems[containerList.Index]);
							playerTokens.Add(containerTokens[containerList.Index]);
							playerList.Items.Add(containerList.Items[containerList.Index]);
							containerItems.RemoveAt(containerList.Index);
							containerTokens.RemoveAt(containerList.Index);
							containerList.Items.RemoveAt(containerList.Index);
							//If this was the bottom-most item, adjust our selection.
							if (containerList.Index >= containerList.Items.Count)
								containerList.Index--;
							//If this was the last item, hide the list entirely and switch to the player's side.
							//We know that's possible -- after all, there must be at -least- one item in there now.
							if (containerList.Items.Count == 0)
							{
								containerList.Hidden = true;
								keys[NoxicoGame.KeyBindings[KeyBinding.Right]] = true;
							}
							else
								containerList.Height = (containerList.Items.Count < 10) ? containerList.Items.Count : 10;
							playerList.Hidden = false; //always the case.
							playerList.Height = (playerList.Items.Count < 10) ? playerList.Items.Count : 10;
							containerWindow.Height = containerList.Height + 2;
							playerWindow.Height = playerList.Height + 2;
							capacity.Text = player.Character.Carried + "/" + player.Character.Capacity;
							if (mode == ContainerMode.Vendor)
								capacity.Text = i18n.Format("inventory_money", vendorChar.Name.ToString(), vendorChar.GetToken("money").Value, player.Character.GetToken("money").Value);
							containerList.Change(s, e);
							NoxicoGame.DrawStatus(); 
							UIManager.Draw();
						}
						else
						{
							//There was some error returned by TryRetrieve, which we'll show now.
							MessageBox.Notice(tryAttempt);
						}
					};
				}
				//FIXME: same with this check.
				if (playerList != null)
				{
					//We do basically the same thing as above in reverse.
					playerList.Change = (s, e) =>
					{
						if (playerList.Items.Count == 0)
							return;
						indexRight = playerList.Index;
						var t = playerTokens[playerList.Index];
						var i = playerItems[playerList.Index];
						descriptionWindow.Text = i.ToString(t, false, false);
						var desc = i.GetDescription(t);
						price = 0;
						if (mode == ContainerMode.Vendor && i.HasToken("price"))
						{
							price = i.GetToken("price").Value;
							desc += "\n" + i18n.Format("inventory_itcosts", price);
						}
						//This is one of the few differences.
						if (t.Path("cursed/path") != null)
							desc += "\nThis item is cursed and can't be removed."; //DO NOT TRANSLATE -- Curses will be replaced with better terms and variants such as "Slippery" or "Sticky".
						else if (i.HasToken("equipable") && t.HasToken("equipped"))
							desc += "\n" + i18n.GetString("inventory_youusethis");
						description.Text = Toolkit.Wordwrap(desc.SmartQuote(), description.Width);
						descriptionWindow.Draw();
						description.Draw();
						capacity.Draw();
					};
					playerList.Enter = (s, e) =>
					{
						if (playerList.Items.Count == 0)
							return; 
						//onLeft = false;
						var tryAttempt = TryStore(player, playerTokens[playerList.Index], playerItems[playerList.Index]);
						if (tryAttempt.IsBlank())
						{
							containerItems.Add(playerItems[playerList.Index]);
							containerTokens.Add(playerTokens[playerList.Index]);
							containerList.Items.Add(playerList.Items[playerList.Index]);
							playerItems.RemoveAt(playerList.Index);
							playerTokens.RemoveAt(playerList.Index);
							playerList.Items.RemoveAt(playerList.Index);
							if (playerList.Index >= playerList.Items.Count)
								playerList.Index--;
							if (playerList.Items.Count == 0)
							{
								playerList.Hidden = true;
								keys[NoxicoGame.KeyBindings[KeyBinding.Left]] = true;
							}
							else
								playerList.Height = (playerList.Items.Count < 10) ? playerList.Items.Count : 10;
							containerList.Hidden = false; //always the case.
							containerList.Height = (containerList.Items.Count < 10) ? containerList.Items.Count : 10;
							containerWindow.Height = containerList.Height + 2;
							playerWindow.Height = playerList.Height + 2;
							capacity.Text = player.Character.Carried + "/" + player.Character.Capacity;
							if (mode == ContainerMode.Vendor)
								capacity.Text = i18n.Format("inventory_money", vendorChar.Name.ToString(), vendorChar.GetToken("money").Value, player.Character.GetToken("money").Value);
							playerList.Change(s, e);
							NoxicoGame.DrawStatus();
							UIManager.Draw();
						}
						else
						{
							//This is set by TryStore.
							if (vendorCaughtYou)
							{
								//Immediately break out of ContainerMan and call out.
								NoxicoGame.ClearKeys();
								NoxicoGame.Immediate = true;
								NoxicoGame.Me.CurrentBoard.Redraw();
								//NoxicoGame.Me.CurrentBoard.Draw(true);
								NoxicoGame.Mode = UserMode.Walkabout;
								Subscreens.FirstDraw = true;
								SceneSystem.Engage(player.Character, vendorChar, "(criminalscum)");
							}
							else
								MessageBox.Notice(tryAttempt);
						}
					};
				}

				UIManager.Highlight = containerList.Items.Count > 0 ? containerList : playerList;
				UIManager.Draw();
				if (UIManager.Highlight.Change != null)
					UIManager.Highlight.Change(null, null);
				Subscreens.FirstDraw = false;
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.Me.CurrentBoard.Redraw();
				//NoxicoGame.Me.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else if (NoxicoGame.IsKeyDown(KeyBinding.Left))
			{
				NoxicoGame.ClearKeys();
				if (containerList.Items.Count == 0)
					return; 
				UIManager.Highlight = containerList ?? playerList;
				containerList.DrawQuick();
				containerList.Change(null, null);
				playerList.DrawQuick();
			}
			else if (NoxicoGame.IsKeyDown(KeyBinding.Right))
			{
				NoxicoGame.ClearKeys();
				if (playerList.Items.Count == 0)
					return; 
				UIManager.Highlight = playerList ?? containerList;
				playerList.Change(null, null);
				containerList.DrawQuick();
				playerList.DrawQuick();
			}
			else
				UIManager.CheckKeys();
		}

		/// <summary>
		/// Take something OUT of a container, or BUY it from a vendor.
		/// </summary>
		/// <param name="boardchar">The container/vendor</param>
		/// <param name="token">The item token</param>
		/// <param name="chosen">The item's definition</param>
		/// <returns>Return a failure message or null.</returns>
		private static string TryRetrieve(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			var inv = boardchar.Character.GetToken("items");
			var con = other;
			if (token.HasToken("cursed"))
			{
				//Reveal the cursed item as such if we didn't already know.
				if (!token.GetToken("cursed").HasToken("known"))
					token.GetToken("cursed").AddToken("known");
				return mode == ContainerMode.Vendor ? "It's cursed. " + vendorChar.Name.ToString() + " can't unequip it." : "It's cursed. You shouldn't touch this."; //DO NOT TRANSLATE -- Curses will be replaced with better terms and variants such as "Slippery" or "Sticky".
			}
			if (token.HasToken("equipped"))
			{
				//If we're looting a corpse's equipment, just unequip it.
				/* Cannot happen if we only allow buying for_sale items
				//If a vendor is wearing it though...
				if (mode == ContainerMode.Vendor)
					return i18n.Format("inventory_vendorusesthis", vendorChar.Name.ToString());
				else
				*/
				token.RemoveToken("equipped");
			}
			if (mode == ContainerMode.Vendor && price > 0)
			{
				//Handle the transaction.
				var pMoney = boardchar.Character.GetToken("money");
				var vMoney = vendorChar.GetToken("money");
				//TODO: add charisma and relationship bonuses -- look good for free food, or get a friends discount.
				if (pMoney.Value - price < 0)
					return i18n.GetString("inventory_youcantaffordthis");
				vMoney.Value += price;
				pMoney.Value -= price;
				token.RemoveToken("for_sale");
			}
			inv.Tokens.Add(token);
			con.Tokens.Remove(token);
			boardchar.ParentBoard.Redraw();
			//boardchar.ParentBoard.Draw();
			boardchar.Character.CheckHasteSlow();
			NoxicoGame.Sound.PlaySound("set://GetItem");
			return null;
		}

		/// <summary>
		/// Put something IN a container, or SELL it to a vendor.
		/// </summary>
		/// <param name="boardchar">The container/vendor</param>
		/// <param name="token">The item token</param>
		/// <param name="chosen">The item's definition</param>
		/// <returns>Returns a failure message or null.</returns>
		/// <remarks>Sets vendorCaughtYou when you're being criminal scum.</remarks>
		private static string TryStore(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			var inv = boardchar.Character.GetToken("items");
			var con = other;
			if (token.HasToken("cursed"))
			{
				//Reveal the cursed item as such if we didn't already know.
				if (!token.GetToken("cursed").HasToken("known"))
					token.GetToken("cursed").AddToken("known");
				return "It's cursed! You can't unequip it."; //DO NOT TRANSLATE -- Curses will be replaced with better terms and variants such as "Slippery" or "Sticky".
			}
			if (token.HasToken("equipped"))
			{
				//You should probably switch over to the Inventory screen and take the thing off.
				return i18n.GetString("inventory_youareusingthis");
			}
			if (mode == ContainerMode.Vendor && token.HasToken("owner") && token.GetToken("owner").Text == vendorChar.Name.ToID())
			{
				//We tried to sell the vendor's own crap back to them.
				vendorCaughtYou = true; //Handler can deal with this now.
				return i18n.Format("inventory_vendorcaughtyou", vendorChar.Name.ToString()).Viewpoint(vendorChar);
			}
			if (token.HasToken("torn")) price = (float)Math.Ceiling(price * 0.25f); //this ain't worth shit, bruh.
			if (mode == ContainerMode.Vendor && price > 0)
			{
				//Handle the transaction.
				var pMoney = boardchar.Character.GetToken("money");
				var vMoney = vendorChar.GetToken("money");
				if (vMoney.Value - price < 0)
					return i18n.Format("inventory_vendorcantaffordthis", vendorChar.Name.ToString());
				//TODO: add charisma and relationship bonuses -- I'll throw in another tenner cos you're awesome.
				//notice that the bonus is determined AFTER the budget check so a vendor won't bug out on that.
				pMoney.Value += price;
				vMoney.Value -= price;
				token.AddToken("for_sale");
			}
			con.Tokens.Add(token);
			inv.Tokens.Remove(token);
			boardchar.ParentBoard.Redraw();
			//boardchar.ParentBoard.Draw();
			boardchar.Character.CheckHasteSlow();
			NoxicoGame.Sound.PlaySound("set://PutItem");
			return null;
		}
	}
}
