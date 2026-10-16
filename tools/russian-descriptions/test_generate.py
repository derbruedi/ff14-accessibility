import unittest
from generate import encode, integer


class EncodingTests(unittest.TestCase):
    def test_cyrillic_utf8(self):
        self.assertEqual(encode('Прочность: 30.'), 'Прочность: 30.'.encode())

    def test_published_xivrus_expression_example(self):
        self.assertEqual(encode('<var 08 E905 ((а)) (()) /var>'),
                         bytes.fromhex('020809E905FF03D0B0FF0103'))

    def test_nested_level_dependent_crafting_efficiency(self):
        encoded = encode('<var 08 E4E94509 ((<var 08 E0E946207965 /var>)) 65 /var>%')
        self.assertIn(bytes.fromhex('E4E94509'), encoded)
        self.assertIn(bytes.fromhex('E0E946207965'), encoded)
        self.assertTrue(encoded.endswith(bytes.fromhex('6503') + b'%'))

    def test_line_break_is_game_newline_macro(self):
        self.assertEqual(encode('A\nB'), b'A\x02\x10\x01\x03B')

    def test_unclosed_payload_is_rejected(self):
        with self.assertRaises(ValueError): encode('<var 08 E905 ((broken')

    def test_literal_closing_parenthesis_in_string_expression(self):
        self.assertIn(b'(test)', encode('<var 08 E905 (((test))) (()) /var>'))

    def test_large_length_encoding(self):
        self.assertEqual(integer(256), bytes.fromhex('F101'))
        self.assertEqual(integer(257), bytes.fromhex('F20101'))


if __name__ == '__main__': unittest.main()
