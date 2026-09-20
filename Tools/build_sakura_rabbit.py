# Compatibility entry point for the current spring monster model.
import pathlib, runpy, sys, shutil
name = 'SakuraRabbit'
sys.argv = [sys.argv[0], '--', name]
runpy.run_path('Tools/replace_spring_monster_models.py', run_name='__main__')
staged = pathlib.Path('Temp/MonsterReplacement') / name
for filename in [name + '.fbx', 'base_color.png', 'normal.png']:
    shutil.copy2(staged / filename, pathlib.Path('Assets/Art/Enemies') / name / filename)
shutil.copy2(staged / (name + '_Rigged.blend'), pathlib.Path('ArtSources') / name / (name + '_Rigged.blend'))
print('Reimport in Unity, then run SpringMonsterModelRefresh.RefreshAll().')