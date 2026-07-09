namespace BasketballManager;

public static class DefaultDemoData
{
    public const string CompetitionId = "demo001";
    public const string CompetitionName = "内置演示赛事";

    public static void Seed(DataStore store)
    {
        var data = new AppData { SchemaVersion = 6 };

        var fieldPos = new PlayerFieldDefinition { Name = "位置", FieldType = "Text", DisplayOrder = 0 };
        var fieldHeight = new PlayerFieldDefinition { Name = "身高", FieldType = "Text", DisplayOrder = 1 };
        var fieldGrade = new PlayerFieldDefinition { Name = "年级", FieldType = "Text", DisplayOrder = 2 };
        data.PlayerFields.AddRange([fieldPos, fieldHeight, fieldGrade]);

        var home = new Team { Name = "蓝队", Note = "内置主队", Status = "启用" };
        var away = new Team { Name = "红队", Note = "内置客队", Status = "启用" };
        data.Teams.AddRange([home, away]);

        void AddPlayers(Team team, IReadOnlyList<(string Name, string No, string Pos, string Height, string Grade)> rows)
        {
            foreach (var row in rows)
            {
                var player = new Player
                {
                    Name = row.Name,
                    StudentNumber = row.No,
                    TeamId = team.Id,
                    Team = team.Name,
                    Status = "在队",
                    Note = "内置演示球员"
                };
                data.Players.Add(player);
                data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldPos.Id, Value = row.Pos });
                data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldHeight.Id, Value = row.Height });
                data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldGrade.Id, Value = row.Grade });
            }
        }

        AddPlayers(home,
        [
            ("陈思远", "D1001", "得分后卫", "186cm", "大三"),
            ("李明轩", "D1002", "控球后卫", "178cm", "大二"),
            ("赵一诺", "D1003", "小前锋", "190cm", "大一"),
            ("周子墨", "D1004", "中锋", "196cm", "大四"),
            ("孙启航", "D1005", "大前锋", "192cm", "大三"),
            ("林晓博", "D1006", "替补前锋", "188cm", "大二"),
        ]);

        AddPlayers(away,
        [
            ("韩宇泽", "D2001", "得分后卫", "184cm", "大四"),
            ("吴佳怡", "D2002", "控球后卫", "172cm", "大二"),
            ("郑浩然", "D2003", "中锋", "198cm", "大三"),
            ("高子涵", "D2004", "小前锋", "185cm", "大一"),
            ("唐启铭", "D2005", "大前锋", "191cm", "大二"),
            ("何子安", "D2006", "替补后卫", "180cm", "大三"),
        ]);

        // No matches by design: only roster-ready demo teams/players.
        store.Save(data);
    }
}
