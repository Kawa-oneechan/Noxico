using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;


namespace Noxico
{
	public class Inventory
	{
		private static int selection = 0;
		//Split up the Dictionary for easier access to both halves. It was a silly setup anyway.
		private static List<Token> inventoryTokens = new List<Token>();
		private static List<InventoryItem> inventoryItems = new List<InventoryItem>();
		private static UIList itemList;
		private static UILabel howTo, itemDesc;
		private static UILabel capacity;
		private static UILabel sigilView;
		private static UIWindow yourWindow, descriptionWindow;
		private static List<string> sigils;

		private static void TryUse(Character character, Token token, InventoryItem chosen)
		{
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			itemList.Enabled = false;
			chosen.Use(character, token);
			Subscreens.Redraw = true;
		}

		private static void TryDrop(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			itemList.Enabled = false;
			if (token.HasToken("equipped"))
				try
				{
					chosen.Unequip(boardchar.Character, token);
				}
				catch (ItemException x)
				{
					MessageBox.Notice(x.Message.Viewpoint(boardchar.Character));
				}
			if (!token.HasToken("equipped"))
			{
				NoxicoGame.AddMessage(i18n.Format("dropped_x", chosen.ToString(token, true, true)));
				NoxicoGame.Sound.PlaySound("set://PutItem");
				chosen.Drop(boardchar, token);
				NoxicoGame.Me.CurrentBoard.Update();
				//NoxicoGame.Me.CurrentBoard.Draw();
				NoxicoGame.Subscreen = Inventory.Handler;
				Subscreens.Redraw = true;
			}
			itemList.Enabled = true;
		}

		private static void UpdateColumns()
		{
			sigilView.Text = "";
			for (var row = 0; row < itemList.Height; row++)
			{
				var index = row + itemList.Scroll;
				if (index < sigils.Count)
				{
					sigilView.Text += sigils[index] + "\n";
				}
			}
			sigilView.Text.TrimEnd();
			sigilView.Draw();
		}

		public static void Handler()
		{
			var player = NoxicoGame.Me.Player;
			if (!player.Character.HasToken("items") || player.Character.GetToken("items").Tokens.Count == 0)
			{
				MessageBox.Notice(i18n.GetString("inventory_youhavenothing"), true);
				Subscreens.PreviousScreen.Clear();
				Subscreens.FirstDraw = true;
				return;
			}

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;

				inventoryTokens.Clear();
				inventoryItems.Clear();
				var itemTexts = new List<string>();
				Inventory.sigils = new List<string>();
				foreach (var carriedItem in player.Character.GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					inventoryTokens.Add(carriedItem);
					inventoryItems.Add(find);

					var item = find;
					var sigils = new List<string>();
					var carried = carriedItem;
					var icon = " ";

					if (item.HasToken("ascii"))
					{
						var color = "Silver";
						if (item.Path("ascii/fore") != null)
							color = item.Path("ascii/fore").Text;
						if (carriedItem.HasToken("color"))
							color = carriedItem.GetToken("color").Text;
						if (item.ID == "book")
						{
							var cga = new[] { "Black", "DarkBlue", "DarkGreen", "DarkCyan", "DarkRed", "Purple", "Brown", "Silver", "Gray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White" };
							color = cga[carriedItem.GetToken("id").Text.GetHashCode() % cga.Length];
						}
						if (color.Equals("black", StringComparison.OrdinalIgnoreCase))
							color = "Gray";
						icon = "<c" + color + ">" + (char)item.Path("ascii/char").Value;
					}

					if (item.HasToken("equipable"))
					{
						var eq = item.GetToken("equipable");
						//if (item.HasToken("weapon"))
						//	sigils.Add("weapon");
						if (eq.HasToken("hat") && eq.HasToken("goggles") && eq.HasToken("mask"))
							sigils.Add("fullmask");
						else
						{
							foreach (var x in new[] { "hat", "goggles", "mask" })
								if (eq.HasToken(x))
									sigils.Add(x);
						}
						foreach (var x in new[] { "neck", "ring" })
							if (eq.HasToken(x))
								sigils.Add(x);
						if (eq.HasToken("underpants") || eq.HasToken("undershirt"))
							sigils.Add("undies");
						if (eq.HasToken("shirt") && !eq.HasToken("pants"))
							sigils.Add("shirt");
						if (eq.HasToken("pants") && !eq.HasToken("shirt"))
							sigils.Add("pants");
						if (eq.HasToken("shirt") && eq.HasToken("pants"))
							sigils.Add("suit");
						foreach (var x in new[] { "shoes", "jacket", "cloak", "socks" })
							if (eq.HasToken(x))
								sigils.Add(x);
						if (carried.HasToken("equipped"))
							sigils.Add("equipped");
					}
					if (carried.HasToken("unidentified"))
						sigils.Add("unidentified");
					/*
					if (item.HasToken("statbonus"))
					{
						foreach (var bonus in item.GetToken("statbonus").Tokens)
						{
							if (bonus.Name == "health")
								sigils.Add("\uE300" + bonus.Value + "HP");
						}
					}
					*/
					var info = item.GetModifiers(carriedItem);
					sigils.AddRange(info.Select(x => "\uE300" + x));

					/* Removed -- should be replaced with less cursy words.
#if DEBUG
					if (carried.HasToken("cursed"))
						sigils.Add(carried.GetToken("cursed").HasToken("known") ? "cursed" : carried.GetToken("cursed").HasToken("hidden") ? "(cursed)" : "cursed!");
#else
					if (carried.HasToken("cursed") && !carried.GetToken("cursed").HasToken("hidden") && carried.GetToken("cursed").HasToken("known"))
						sigils.Add("cursed");
#endif
					*/

					var itemString = item.ToString(carried, false, false);
					if (itemString.Length > 33)
						itemString = itemString.Disemvowel();
					itemTexts.Add(itemString);
					var sigilText = new StringBuilder();
					var sigilComma = string.Empty;
					for (var i = 0; i < sigils.Count; i++)
					{
						if (sigilText.Length < 30)
						{
							sigilText.Append(sigilComma);
							sigilText.Append((sigils[i][0] == '\uE300') ? sigils[i].Substring(1) : i18n.GetString("sigil_" + sigils[i]));
							sigilComma = ", ";
						}
						else
						{
							if (i < sigils.Count)
								sigilText.Append('\u0137');
							break;
						}
					}
					Inventory.sigils.Add(icon + "<cGray> " + sigilText.ToString());
					/* Inventory.sigils.Add(icon + "<cGray> " + string.Join(", ", sigils.Select(s =>
					{
						if (s[0] == '\uE300')
							return s.Substring(1);
						else
							return i18n.GetString("sigil_" + s);
					}))); */
				}
				var height = inventoryItems.Count;
				if (height > 10)
					height = 10;
				if (selection >= inventoryItems.Count)
					selection = inventoryItems.Count - 1;

				if (UIManager.Elements.Count < 2)
				{
					descriptionWindow = new UIWindow(string.Empty) { Left = 2, Top = 14, Width = 76, Height = 6, Title = UIColors.RegularText };
					howTo = new UILabel("") { Left = 0, Top = 0, Width = 79, Height = 1, Background = UIColors.StatusBackground, Foreground = UIColors.StatusForeground };
					itemDesc = new UILabel("") { Left = 4, Top = 15, Width = 72, Height = 5 };
					sigilView = new UILabel("") { Left = 35, Top = 2, Width = 60, Height = height };
					itemList = new UIList("", null, itemTexts) { Left = 2, Top = 2, Width = 76, Height = height, Index = selection, Background = UIColors.WindowBackground };
					itemList.Change = (s, e) =>
					{
						selection = itemList.Index;

						var t = inventoryTokens[itemList.Index];
						var i = inventoryItems[itemList.Index];
						var r = string.Empty;
						var d = i.GetDescription(t) + "\n\n";

						var weaponData = i.GetToken("weapon");
						if (weaponData != null)
						{
							if (weaponData.HasToken("skill"))
								d += i18n.Format("inventory_weaponskill", weaponData.GetToken("skill").Text.Replace('_', ' ') + "    ");
							if (weaponData.HasToken("attacktype"))
								d += i18n.Format("inventory_weapontype", i18n.GetString("attacktype_" + weaponData.GetToken("attacktype").Text));
							d += '\n';
						}
						var mods = i.GetModifiers(t);
						if (mods.Count > 0)
						{
							d += i18n.Format("inventory_modifiers", string.Join(", ", mods));
						}

						d = Toolkit.Wordwrap(d, itemDesc.Width);

						if (i.ID == "book")
							r = i18n.GetString("inventory_pressenter_book");
						else if (i.HasToken("equipable"))
						{
							if (t.HasToken("equipped"))
							{
								if (t.Path("cursed/known") != null)
									r = i18n.GetString("inventory_cannotunequip");
								else
									r = i18n.GetString("inventory_pressenter_unequip");
							}
							else
								r = i18n.GetString("inventory_pressenter_equip");
						}
						else if (i.HasToken("quest"))
							r = i18n.GetString("inventory_questitem");
						else
							r = i18n.GetString("inventory_pressenter_use");

						howTo.Text = (' ' + r).PadEffective(80);
						itemDesc.Text = d;
						descriptionWindow.Text = i.ToString(t, false, false);
						//howTo.Draw();
						//itemDesc.Draw();
						UpdateColumns();
						UIManager.Draw();
					};
					itemList.Enter = (s, e) =>
					{
						TryUse(player.Character, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
					};
					capacity = new UILabel(player.Character.Carried + "/" + player.Character.Capacity) { Left = 6, Top = 19 };
					yourWindow = new UIWindow(i18n.GetString("inventory_yours")) { Left = 1, Top = 1, Width = 78, Height = 2 + height };
					UIManager.Elements.Add(new UILabel(new string(' ', 80)) { Left = 0, Top = 49, Background = UIColors.StatusBackground });
					UIManager.Elements.Add(yourWindow);
					UIManager.Elements.Add(descriptionWindow);
					UIManager.Elements.Add(howTo);
					UIManager.Elements.Add(itemList);
					UIManager.Elements.Add(sigilView);
					UIManager.Elements.Add(itemDesc);
					UIManager.Elements.Add(capacity);
					UIManager.Elements.Add(new UIButton(' ' + i18n.GetString("inventory_drop") + ' ', (s, e) => { TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]); }) { Left = 76 - i18n.GetString("inventory_drop").Length() - 2, Top = 1 });
					UIManager.Highlight = itemList;
				}
				else
				{
					yourWindow.Height = 2 + height;
					itemList.Items = itemTexts;
					NoxicoGame.Me.CurrentBoard.Redraw();
					NoxicoGame.Me.CurrentBoard.Draw();
				}
				itemList.Index = selection;

				NoxicoGame.DrawSidebar();
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || NoxicoGame.IsKeyDown(KeyBinding.Items) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.Me.CurrentBoard.Redraw();
				NoxicoGame.Me.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			//else if (NoxicoGame.IsKeyDown(KeyBinding.Drop))
			//{
			//	TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
			//}
			else
			{
				UIManager.CheckKeys();
				sigilView.Draw();
			}
		}
	}

}
