import base64
import gzip
import json
from pathlib import Path
import unittest

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]

def read(path):
    raw=path.read_bytes()
    if path.suffix=='.gz':raw=gzip.decompress(raw)
    return json.loads(raw.decode('utf-8-sig'))

class GameTextTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source=read(HERE/'game-text-sources.json.gz')
        cls.catalog=read(ROOT/'FF14Accessibility/Resources/RussianActionNames.json.gz')

    def test_all_player_and_job_actions_have_their_own_translation(self):
        for group in ('player_action_ids','job_action_ids'):
            self.assertGreater(len(self.source[group]),1000)
            for key in self.source[group]:
                self.assertIn(str(key),self.catalog['ActionName'],f'{group}/{key}')

    def test_every_accepted_translation_matches_its_exact_source(self):
        for table,rows in self.catalog.items():
            for key,pair in rows.items():
                self.assertEqual(self.source['sheets'][table][key]['bytes'],pair[0],f'{table}/{key}')
                self.assertTrue(base64.b64decode(pair[1]).strip(),f'{table}/{key}')

    def test_all_captured_text_including_items_and_actions_is_translated(self):
        for table,rows in self.source['sheets'].items():
            missing=[key for key,row in rows.items() if not row['text'].startswith('_rsv_') and key not in self.catalog[table]]
            self.assertEqual([],missing,table)

    def test_server_keys_never_become_fake_names(self):
        for table,rows in self.source['sheets'].items():
            for key,row in rows.items():
                if row['text'].startswith('_rsv_'):
                    self.assertNotIn(key,self.catalog[table],f'{table}/{key}')

    def test_same_english_word_keeps_its_crafting_and_combat_meanings(self):
        for table,row,expected in [('CraftActionName','100387','Размышление'),
                                   ('ActionName','425','Отражение'),
                                   ('StatusName','518','Отражение'),
                                   ('EmoteName','82','Размышление')]:
            self.assertEqual('Reflect',self.source['sheets'][table][row]['text'])
            self.assertEqual(expected,base64.b64decode(self.catalog[table][row][1]).decode())

    def test_dungeon_conditional_description_keeps_both_game_choices(self):
        pair=self.catalog['DeepDungeonItemTooltip']['38']
        source=base64.b64decode(pair[0]);target=base64.b64decode(pair[1])
        # These are the branch opcodes and the dungeon parameter (109), not
        # hardcoded prose about a floor. The two Russian branches must differ.
        for marker in (bytes.fromhex('E4E96E05'),bytes.fromhex('E96E')):
            self.assertIn(marker,source)
            self.assertIn(marker,target)
        self.assertIn('следующем этаже'.encode(),target)
        self.assertIn('следующей области'.encode(),target)

if __name__=='__main__':unittest.main()
