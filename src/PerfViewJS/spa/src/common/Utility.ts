export const transformStringArrayToDetailListItems = (items: string[]) =>
  items.map((item, i) => ({
    key: i,
    name: item,
    value: item,
  }));
