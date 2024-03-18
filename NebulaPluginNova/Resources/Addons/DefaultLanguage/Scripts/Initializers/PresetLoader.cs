//プリセットを登録
Virial.NebulaAPI.RegisterPreset("preset.damned.legacy","旧版NoS \"呪われし者\" プリセット","旧版NoSの役職『呪われし者』の設定に沿ったプリセット","options.role.damned",()=>{
	//Damnedの追加役職定義を取得
	var damned = Virial.NebulaAPI.GetModifier("damned");
	//Crewmateの役職定義を取得
	var crewmate = Virial.NebulaAPI.GetRole("crewmate");
	
	//Damnedの役職フィルターを、"クルーメイトのみ"に設定
	damned.RoleFilter.Filter(Virial.Configuration.FilterAction.Set, crewmate);
	//キルを仕掛けた相手の役職を乗っ取る設定をOffに
	damned.GetConfiguration("options.role.damned.takeOverRoleOfKiller")?.UpdateValue(false);
	//キルを仕掛けた相手を殺す設定をOffに
	damned.GetConfiguration("options.role.damned.damnedMurderMyKiller")?.UpdateValue(false);
	
	//Damnedがインポスター、第三陣営に付与されないように
	damned.GetConfiguration("options.role.damned.impostorCount")?.UpdateValue(0);
	damned.GetConfiguration("options.role.damned.neutralCount")?.UpdateValue(0);
	
	//Damnedがクルーメイトに割り当てられない設定の場合はひとまず1人割り当てるように変更
	var damnedCount = damned.GetConfiguration("options.role.damned.crewmateCount");
	if(damnedCount?.AsInt() == 0) damnedCount?.UpdateValue(1);
	
	//クルーメイトが割り当てられない設定の場合はひとまず1人割り当てるように変更
	var crewCount = crewmate.GetConfiguration("options.role.crewmate.count");
	if(crewCount?.AsInt() == 0) crewCount?.UpdateValue(1);
});