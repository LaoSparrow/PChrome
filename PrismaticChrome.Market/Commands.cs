﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LazyUtils;
using LinqToDB;
using PrismaticChrome.Core;
using TShockAPI;

namespace PrismaticChrome.Market
{
    internal class Commands
    {
        private const int pagelimit = 20;

        private static TSPlayer GetOnline(string name) =>
            TShock.Players.FirstOrDefault(plr => plr?.Account?.Name == name);

        [Alias("列表"), Permission("economy.market.player")]
        public static void list(CommandArgs args, int page)
        {
            using (var context = Db.Context<ShopItem>())
            {
                var sb = new StringBuilder();
                sb.AppendLine($"当前商店商品({page}/{context.Config.Count() / pagelimit}):");
                sb.Append(string.Join("\n",
                    context.Config.OrderByDescending(d => d.id).Skip((page - 1) * pagelimit).Take(pagelimit)));
                args.Player.SendSuccessMessage(sb.ToString());
            }
        }

        [Alias("列表"), Permission("economy.market.player")]
        public static void list(CommandArgs args) => list(args, 1);

        [Alias("购买"), Permission("economy.market.player"), RealPlayer]
        public static void buy(CommandArgs args, int index)
        {
            using (var context = Db.Context<ShopItem>())
            {
                var item = context.Config.SingleOrDefault(d => d.id == index);
                if (item == null)
                {
                    args.Player.SendErrorMessage("商品索引无效");
                    return;
                }

                using (var query = args.Player.Get<Money>())
                {
                    var money = query.Single().money;
                    if (money < item.price)
                    {
                        args.Player.SendErrorMessage($"拥有的货币不足,还需要{item.price - money}$");
                        return;
                    }

                    if (!args.Player.InventorySlotAvailable)
                    {
                        args.Player.SendErrorMessage("背包已满");
                        return;
                    }

                    query.Set(d => d.money, d => d.money - item.price).Update();
                    item.GiveTo(args.Player);
                    if (string.IsNullOrEmpty(item.owner)) return;

                    using (var query2 = Db.Get<Money>(item.owner))
                        query2.Set(d => d.money, d => d.money + item.price).Update();

                    GetOnline(item.owner)?.SendSuccessMessage($"玩家[{args.Player.Name}]以购买了您的{item}");
                }
            }
        }

        private static void AddShopItem(CommandArgs args, int price, bool infinity)
        {
            var item = args.Player.SelectedItem;
            if (item.IsAir)
            {
                args.Player.SendErrorMessage("你手持物品为空");
                return;
            }

            using (var context = Db.Context<ShopItem>())
            {
                var shopitem = new ShopItem
                {
                    type = item.type,
                    price = price,
                    owner = infinity ? null : args.Player.Account.Name,
                    prefix = item.prefix,
                    stack = item.stack,
                    infinity = infinity
                };
                context.Config.Insert(() => shopitem);
            }

            item.TurnToAir();
            item.Send();
        }

        [Alias("出售"), Permission("economy.market.player"), RealPlayer]
        public static void sell(CommandArgs args, int price)
        {
            AddShopItem(args, price, false);
        }

        [Alias("添加"), Permission("economy.market.admin"), RealPlayer]
        public static void add(CommandArgs args, int price)
        {
            AddShopItem(args, price, true);
        }

        [Alias("删除"), Permission("economy.market.player"), RealPlayer]
        public static void del(CommandArgs args, int index)
        {
            using (var context = Db.Context<ShopItem>())
            {
                var item = context.Config.SingleOrDefault(d => d.id == index);
                if (item == null)
                {
                    args.Player.SendErrorMessage("商品索引无效");
                    return;
                }

                if (item.owner != args.Player.Account.Name && !args.Player.HasPermission("economy.market.admin"))
                {
                    args.Player.SendErrorMessage("你没有权限删除别人的商品");
                    return;
                }
                
                if (!args.Player.InventorySlotAvailable)
                {
                    args.Player.SendErrorMessage("背包已满");
                    return;
                }
                
                item.GiveTo(args.Player);
                context.Config.Where(d => d.id == index).Delete();
            }
        }

        public static void Default(CommandArgs args)
        {
            args.Player.SendInfoMessage("用法:\n" +
                                        "/shop add <价格>\n" +
                                        "/shop del <商品索引>\n" +
                                        "/shop sell <价格>\n" +
                                        "/shop buy <商品索引>\n" +
                                        "/shop list [页码]");
        }
    }
}
