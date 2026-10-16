import json
from pathlib import Path
import unittest

import item_templates


class ItemNameTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        here=Path(__file__).resolve().parent
        cls.heads=json.loads((here/'item-heads.json').read_text(encoding='utf-8-sig'))
        cls.modifiers=item_templates.load_modifiers()
        cls.direct=json.loads((here/'item-direct-translations.json').read_text(encoding='utf-8-sig'))

    def translate(self,name,categories):
        return self.direct.get(name) or item_templates.translate(name,self.heads,self.modifiers,categories)

    def test_fish_epithets_are_not_materials(self):
        self.assertEqual('Серебряная акула',self.translate('Silver Shark',{47}))
        self.assertEqual('Переливчатая форель',self.translate('Iridescent Trout',{47}))
        self.assertIsNone(item_templates.translate('Silver Shark',self.heads,self.modifiers,{47}))

    def test_source_animal_is_not_replaced_with_its_skin(self):
        self.assertEqual('Молоко альдкозла',self.translate('Aldgoat Milk',{45}))
        self.assertEqual('Коробочка аркейского хлопка',self.translate('AR-Caean Cotton Boll',{50}))

    def test_equipment_preserves_role_upgrade_and_numbers(self):
        value=self.translate('Augmented Ironworks Ring of Healing +1',{41})
        self.assertIsNotNone(value)
        self.assertIn('Улучшенная версия:',value)
        self.assertIn('Гарлонда',value)
        self.assertIn('для исцеления',value)
        self.assertIn('+1',value)
        self.assertIn('уровень предмета 395',self.translate('Deepgold Gear Coffer (IL 395)',{61}))

    def test_belts_are_equipment_but_unknown_material_is_not_guessed(self):
        self.assertIn('из кожи ленивца',self.translate('Slothskin Belt of Fending',{39}))
        self.assertIsNone(self.translate('Unseen Upstream Material Ring of Healing',{41}))


if __name__=='__main__':unittest.main()
