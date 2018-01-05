using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	/// <summary>
	/// Displays the crafting interface.
	/// </summary>
	public class Crafting
	{
		/// <summary>
		/// The character doing the crafting. Set by GetPossibilities and Open.
		/// </summary>
		public static Character Carrier { get; private set; }
		/// <summary>
		/// The Carrier's items. Set by GetPossibilities.
		/// </summary>
		public static Token ItemsToWorkWith { get; private set; }

		/// <summary>
		/// Returns a list of possible Recipes that a given character is able to craft.
		/// </summary>
		/// <param name="carrier">Probably the player character.</param>
		/// <returns>A list of possible recipes. Keep it, and invoke Apply on the player's choice.</returns>
		public static List<Recipe> GetPossibilities(Character carrier)
		{
			Carrier = carrier;
			ItemsToWorkWith = Carrier.GetToken("items");
			var results = new List<Recipe>();
			var tml = Mix.GetTokenTree("crafting.tml");
			foreach (var carriedItem in ItemsToWorkWith.Tokens)
			{
				var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
				if (knownItem == null)
					continue;
				foreach (var craft in tml.Where(t => t.Name == "craft"))
				{
					var requirements = new List<Token>();
					var key = craft.GetToken("key").Tokens[0];
					var craftOkay = true;
					var considerations = new List<Token>();
					if (key.Name.StartsWith("@"))
					{
						var r = key.Name.Substring(1);
						if (!knownItem.HasToken(r))
							continue;
					}
					else if (key.Name != knownItem.ID)
						continue;

					var repeat = false;
					do
					{
						requirements.Clear();
						requirements.Add(carriedItem);
						repeat = false;
						foreach (var requirement in craft.GetToken("require").Tokens)
						{
							if (requirement.Name.StartsWith("@"))
							{
								var r = requirement.Name.Substring(1);
								if (r[0] == '-')
								{
									//simple negatory token check
									var firstValidItem = ItemsToWorkWith.Tokens.Find(i2ww => NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == i2ww.Name && !ki.HasToken(r)) != null && !considerations.Contains(i2ww));
									if (firstValidItem == null)
									{
										craftOkay = false;
										break;
									}
									requirements.Add(firstValidItem);
									considerations.Add(firstValidItem);
									repeat = true;
								}
								else if (r.Contains('-') || r.Contains('+'))
								{
									//complicated token check
									var fuckery = r.Replace("+", ",").Replace("-", ",-").Split(',');
									var checkedKnowns = new List<InventoryItem>();
									foreach (var ki in NoxicoGame.KnownItems)
									{
										var includeThis = false;
										foreach (var fucking in fuckery)
										{
											if (fucking[0] != '-' && ki.HasToken(fucking))
												includeThis = true;
											else if (ki.HasToken(fucking.Substring(1)))
											{
												includeThis = false;
												break;
											}
										}
										if (includeThis)
											checkedKnowns.Add(ki);
									}
									var firstValidItem = ItemsToWorkWith.Tokens.Find(i2ww => checkedKnowns.FirstOrDefault(ki => ki.ID == i2ww.Name) != null && !considerations.Contains(i2ww));
									if (firstValidItem == null)
									{
										craftOkay = false;
										break;
									}
									requirements.Add(firstValidItem);
									considerations.Add(firstValidItem);
									repeat = true;
								}
								else
								{
									//simple token check
									var firstValidItem = ItemsToWorkWith.Tokens.Find(i2ww => NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == i2ww.Name && ki.HasToken(r)) != null && !considerations.Contains(i2ww));
									if (firstValidItem == null)
									{
										craftOkay = false;
										break;
									}
									requirements.Add(firstValidItem);
									considerations.Add(firstValidItem);
									repeat = true;
								}
							}
							else
							{
								if (!ItemsToWorkWith.HasToken(requirement.Name))
								{
									craftOkay = false;
									break;
								}
								requirements.Add(ItemsToWorkWith.GetToken(requirement.Name));
							}
						}

						if (!craftOkay)
							continue;

						var recipe = new Recipe();
						var actions = craft.GetToken("actions");
						foreach (var action in actions.Tokens)
						{
							if (action.Name == "consume")
							{
								var itemIndex = (int)action.Value - 1;
								if (itemIndex >= requirements.Count)
								{
									Program.WriteLine("Warning: recipe wants to consume out-of-bounds requirement.");
									continue;
								}
								recipe.Actions.Add(new CraftConsumeItemAction() { Target = requirements[itemIndex] });
							}
							else if (action.Name == "produce")
							{
								var itemMade = new Token(action.Text);
								itemMade.Tokens.AddRange(action.Tokens);
								recipe.Actions.Add(new CraftProduceItemAction() { Target = itemMade });
								knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == itemMade.Name);
								recipe.Display = i18n.Format("craft_produce_x", knownItem.ToString(itemMade));
							}
							else if (action.Name == "dye")
							{
								//var color = NoxicoGame.KnownItems.Find(ki => ki.ID == requirements[0].Name).GetToken("color").Text;
								var color = requirements[0].GetToken("color").Text;
								knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == requirements[1].Name);
								recipe.Display = i18n.Format("craft_dye_x_y", knownItem.ToString(requirements[1]), color);
								if (!requirements[1].HasToken("color"))
									recipe.Actions.Add(new CraftAddTokenAction() { Target = requirements[1], Add = new Token("color", 0, color) });
								else if (requirements[1].GetToken("color").Text == color)
									recipe.Display = null;
								else
									recipe.Actions.Add(new CraftChangeTokenAction() { Target = requirements[1].GetToken("color"), NewText = color, NewValue = requirements[1].Value });
							}
						}
						if (string.IsNullOrWhiteSpace(recipe.Display))
							continue;
						if (results.Exists(x => x.Display == recipe.Display))
							continue;
						results.Add(recipe);
					} while (repeat);
				}
			}
			return results;
		}

		private static UIWindow recipeWindow;
		private static UIList recipeList;
		private static List<Recipe> recipes;

		/// <summary>
		/// Generic Subscreen handler.
		/// </summary>
		public static void Handler()
		{
			//TODO: add more things, such as a status bar and a window with the exact results.
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
				recipes = GetPossibilities(Carrier);
				var h = recipes.Count < 40 ? recipes.Count : 40;
				if (h == 0)
					h++;
				recipeWindow = new UIWindow(string.Empty) { Top = 2, Left = 2, Width = 76, Height = h + 2 };
				recipeList = new UIList(string.Empty, null, recipes.Select(r => r.Display)) { Top = 3, Left = 3, Width = 74, Height = h };
				recipeList.Enter = (s, e) =>
				{
					if (recipes.Count == 0)
						return;
					var i = recipeList.Index;
					recipes[i].Apply();
					//Redetermine the possibilities and update the UI accordingly.
					recipes = GetPossibilities(Carrier);
					if (recipes.Count > 0)
					{
						recipeList.Items = recipes.Select(r => r.Display).ToList();
						if (i >= recipeList.Items.Count)
							i = recipeList.Items.Count - 1;
						recipeList.Index = i;
						h = recipes.Count < 40 ? recipes.Count : 40;
					}
					else
					{
						h = 1;
						recipeList.Hidden = true;
					}
					recipeWindow.Height = h + 2;
					recipeList.Height = h;
					NoxicoGame.Me.CurrentBoard.Redraw();
					NoxicoGame.Me.CurrentBoard.Draw();
					UIManager.Draw();
				};
				UIManager.Elements.Add(recipeWindow);
				UIManager.Elements.Add(new UILabel(i18n.GetString("craft_nothing")) { Left = 3, Top = 3 });
				UIManager.Elements.Add(recipeList);
				UIManager.Highlight = recipeList;
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
			else
				UIManager.CheckKeys();
		}

		/// <summary>
		/// Sets up a Crafting subscreen for the given Character, usually the Player.
		/// </summary>
		/// <param name="carrier"></param>
		public static void Open(Character carrier)
		{
			Carrier = carrier;
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

	/// <summary>
	/// Defines a single crafting recipe, a single resulting item.
	/// </summary>
	public class Recipe
	{
		/// <summary>
		/// The display name of this recipe's result.
		/// </summary>
		public string Display { get; set; }
		/// <summary>
		/// A list of CraftActions that produce this recipe's result.
		/// </summary>
		public List<CraftAction> Actions { get; set; }
		public Recipe()
		{
			Actions = new List<CraftAction>();
		}
		/// <summary>
		/// Simply applies each CraftAction for this Recipe.
		/// </summary>
		public void Apply()
		{
			Actions.ForEach(a => a.Apply());
		}
		public override string ToString()
		{
			return Display;
		}
	}

	/// <summary>
	/// Base class for CraftActions.
	/// </summary>
	public abstract class CraftAction
	{
		/// <summary>
		/// The Token that this CraftAction applies to.
		/// </summary>
		public Token Target { get; set; }
		/// <summary>
		/// What to do with the Target.
		/// </summary>
		public abstract void Apply();
	}
	/// <summary>
	/// Adds the CraftAction's Target to the Carrier's item list.
	/// </summary>
	public class CraftProduceItemAction : CraftAction
	{
		public override void Apply()
		{
			Crafting.ItemsToWorkWith.AddToken(Target);
		}
	}
	/// <summary>
	/// Consumes an item with the Target's name in the Carrier's inventory.
	/// </summary>
	public class CraftConsumeItemAction : CraftAction
	{
		public override void Apply()
		{
			var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == Target.Name);
			knownItem.Consume(Crafting.Carrier, Target);
		}
	}
	/// <summary>
	/// Changes the Target's Text and/or Value.
	/// </summary>
	public class CraftChangeTokenAction : CraftAction
	{
		public string NewText { get; set; }
		public float NewValue { get; set; }
		public override void Apply()
		{
			Target.Text = NewText;
			Target.Value = NewValue;
		}
	}
	/// <summary>
	/// Removes a Token from the Target's children.
	/// </summary>
	public class CraftRemoveTokenAction : CraftAction
	{
		public Token Remove { get; set; }
		public override void Apply()
		{
			Target.RemoveToken(Remove);
		}
	}
	/// <summary>
	/// Adds a Token to the Target's children.
	/// </summary>
	public class CraftAddTokenAction : CraftAction
	{
		public Token Add { get; set; }
		public override void Apply()
		{
			Target.AddToken(Add);
		}
	}
}
